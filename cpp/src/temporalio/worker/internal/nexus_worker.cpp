#include "temporalio/worker/internal/nexus_worker.h"

#include <any>
#include <span>
#include <stdexcept>
#include <string>
#include <thread>
#include <utility>

#include <temporalio/coro/run_sync.h>

#include <temporal/sdk/core/nexus/nexus.pb.h>
#include <temporal/api/common/v1/message.pb.h>
#include <temporal/api/nexus/v1/message.pb.h>
#include <temporal/api/workflowservice/v1/request_response.pb.h>

#include "temporalio/bridge/worker.h"

using temporalio::coro::run_task_sync;

namespace temporalio::worker::internal {

namespace {

/// Build and send a NexusTaskCompletion with a HandlerError to the bridge.
void send_nexus_error_completion(
    bridge::Worker* bridge_worker,
    const std::vector<uint8_t>& task_token,
    const std::string& error_type,
    const std::string& message) {
    if (!bridge_worker) return;

    coresdk::nexus::NexusTaskCompletion completion;
    completion.set_task_token(
        std::string(task_token.begin(), task_token.end()));
    auto* error = completion.mutable_error();
    error->set_error_type(error_type);
    error->mutable_failure()->set_message(message);

    std::string bytes;
    completion.SerializeToString(&bytes);
    std::vector<uint8_t> completion_bytes(bytes.begin(), bytes.end());
    bridge_worker->complete_nexus_task_async(
        std::span<const uint8_t>(completion_bytes),
        [](std::string) {});
}

}  // namespace

NexusWorker::NexusWorker(NexusWorkerOptions options)
    : options_(std::move(options)) {
    // Build service name -> definition lookup map for O(1) dispatch
    for (auto& svc : options_.services) {
        if (svc) {
            services_by_name_[svc->name()] = svc;
        }
    }
}

NexusWorker::~NexusWorker() {
    // Ensure all Nexus task threads are joined before destroying members
    // they reference (running_tasks_mutex_, running_tasks_).
    std::vector<std::shared_ptr<RunningNexusTask>> to_join;
    {
        std::lock_guard lock(running_tasks_mutex_);
        for (auto& [_, running] : running_tasks_) {
            running->cancel_source.request_stop();
            to_join.push_back(running);
        }
    }
    for (auto& running : to_join) {
        if (running->thread.joinable()) {
            running->thread.join();
        }
    }
}

coro::Task<std::optional<std::vector<uint8_t>>>
NexusWorker::poll_nexus_task() {
    if (!options_.bridge_worker) {
        co_return std::nullopt;
    }

    auto tcs = std::make_shared<
        coro::TaskCompletionSource<std::optional<std::vector<uint8_t>>>>();

    options_.bridge_worker->poll_nexus_task_async(
        [tcs](std::optional<std::vector<uint8_t>> result,
              std::string error) {
            if (!error.empty()) {
                tcs->try_set_exception(std::make_exception_ptr(
                    std::runtime_error(
                        "Failed polling Nexus task: " + error)));
            } else {
                tcs->try_set_result(std::move(result));
            }
        });

    co_return co_await tcs->task();
}

coro::Task<void> NexusWorker::complete_nexus_task(
    const std::vector<uint8_t>& completion_bytes) {
    if (!options_.bridge_worker) {
        co_return;
    }

    auto tcs = std::make_shared<coro::TaskCompletionSource<void>>();

    options_.bridge_worker->complete_nexus_task_async(
        std::span<const uint8_t>(completion_bytes),
        [tcs](std::string error) {
            if (error.empty()) {
                tcs->try_set_result();
            } else {
                tcs->try_set_exception(std::make_exception_ptr(
                    std::runtime_error(
                        "Failed completing Nexus task: " + error)));
            }
        });

    co_await tcs->task();
}

coro::Task<void> NexusWorker::execute_async() {
    // Poll loop: continuously poll for Nexus tasks from the bridge.
    // Each task is either a Task (with a PollNexusTaskQueueResponse containing
    // a Request) or CancelTask (cancel a running operation).
    //
    // The flow mirrors C# NexusWorker.ExecuteAsync().
    // WARNING: Nexus support is experimental.

    while (true) {
        auto task_result = co_await poll_nexus_task();

        // Null result means the poller has shut down
        if (!task_result.has_value()) {
            break;
        }

        // Deserialize the NexusTask protobuf from the bridge bytes
        coresdk::nexus::NexusTask nexus_task;
        if (!nexus_task.ParseFromArray(task_result->data(),
                                       static_cast<int>(task_result->size()))) {
            // Malformed task -- nothing we can do (no task token to respond with)
            continue;
        }

        // Handle CancelTask variant
        if (nexus_task.has_cancel_task()) {
            const auto& cancel = nexus_task.cancel_task();
            const auto& ct = cancel.task_token();
            std::vector<uint8_t> cancel_token(ct.begin(), ct.end());
            handle_cancel_task(cancel_token);
            continue;
        }

        // Must be a Task variant (PollNexusTaskQueueResponse)
        if (!nexus_task.has_task()) {
            continue;
        }

        const auto& poll_response = nexus_task.task();
        const auto& token_str = poll_response.task_token();
        std::vector<uint8_t> task_token(token_str.begin(), token_str.end());

        // The poll response contains a Request with either StartOperation or CancelOperation
        if (!poll_response.has_request()) {
            send_nexus_error_completion(
                options_.bridge_worker, task_token,
                "BAD_REQUEST", "NexusTask has no request");
            continue;
        }

        const auto& request = poll_response.request();

        if (request.has_start_operation()) {
            const auto& start_op = request.start_operation();
            const auto& service_name = start_op.service();
            const auto& operation_name = start_op.operation();

            // Look up the service
            auto svc = find_service(service_name);
            if (!svc) {
                send_nexus_error_completion(
                    options_.bridge_worker, task_token,
                    "NOT_FOUND",
                    "Service not found: " + service_name);
                continue;
            }

            // Look up the operation handler
            auto handler = svc->find_operation(operation_name);
            if (!handler) {
                send_nexus_error_completion(
                    options_.bridge_worker, task_token,
                    "NOT_FOUND",
                    "Operation not found: " + operation_name);
                continue;
            }

            // Extract input payload bytes (if any)
            std::vector<uint8_t> input;
            if (start_op.has_payload()) {
                const auto& p = start_op.payload().data();
                input.assign(p.begin(), p.end());
            }

            // Build a fully-populated OperationStartContext from the protobuf
            nexus::OperationStartContext start_ctx;
            start_ctx.service = service_name;
            start_ctx.operation = operation_name;

            // Populate headers from the request
            for (const auto& [key, value] : request.header()) {
                start_ctx.headers[key] = value;
            }

            // Populate StartOperation-specific fields
            start_ctx.request_id = start_op.request_id().empty()
                ? std::nullopt
                : std::optional<std::string>(start_op.request_id());
            start_ctx.callback_url = start_op.callback().empty()
                ? std::nullopt
                : std::optional<std::string>(start_op.callback());

            // Populate callback headers
            for (const auto& [key, value] : start_op.callback_header()) {
                start_ctx.callback_headers[key] = value;
            }

            // Populate inbound links
            for (const auto& link : start_op.links()) {
                nexus::NexusLink nlink;
                nlink.uri = link.url();
                nlink.type = link.type();
                start_ctx.inbound_links.push_back(std::move(nlink));
            }

            handle_start_operation(task_token, handler, input,
                                   std::move(start_ctx));

        } else if (request.has_cancel_operation()) {
            const auto& cancel_op = request.cancel_operation();
            // For a CancelOperation request, we acknowledge it via the bridge.
            // Build a completion with the cancel_operation response variant.
            coresdk::nexus::NexusTaskCompletion completion;
            completion.set_task_token(
                std::string(task_token.begin(), task_token.end()));
            auto* response = completion.mutable_completed();
            response->mutable_cancel_operation();

            std::string bytes;
            completion.SerializeToString(&bytes);
            std::vector<uint8_t> completion_bytes(bytes.begin(), bytes.end());
            if (options_.bridge_worker) {
                options_.bridge_worker->complete_nexus_task_async(
                    std::span<const uint8_t>(completion_bytes),
                    [](std::string) {});
            }
            (void)cancel_op;
        } else {
            send_nexus_error_completion(
                options_.bridge_worker, task_token,
                "BAD_REQUEST", "Unknown Nexus request variant");
        }
    }

    // Poll loop has ended (shutdown). Running tasks will be joined in the destructor.
}

void NexusWorker::handle_start_operation(
    const std::vector<uint8_t>& task_token,
    nexus::INexusOperationHandler* handler,
    const std::vector<uint8_t>& input,
    nexus::OperationStartContext start_ctx) {
    // Create a running task state
    auto running = std::make_shared<RunningNexusTask>();
    running->task_token = task_token;

    {
        std::string token_key(task_token.begin(), task_token.end());
        std::lock_guard lock(running_tasks_mutex_);
        running_tasks_[token_key] = running;
    }

    // Dispatch the operation on a separate thread.
    auto client = options_.client;
    auto ns = options_.ns;
    auto task_queue = options_.task_queue;
    auto bridge_worker = options_.bridge_worker;

    running->thread = std::jthread([this, running, handler, client, ns,
                                    task_queue, bridge_worker,
                                    task_token, input,
                                    start_ctx = std::move(start_ctx),
                                    token_key = std::string(
                                        task_token.begin(),
                                        task_token.end())]() mutable {
        // Set up the Nexus operation execution context
        nexus::NexusOperationInfo op_info;
        op_info.ns = ns;
        op_info.task_queue = task_queue;

        nexus::NexusOperationExecutionContext exec_ctx(
            nexus::OperationContext{start_ctx.service, start_ctx.operation,
                                   start_ctx.headers},
            std::move(op_info), client);
        nexus::ContextScope scope(&exec_ctx);

        try {
            // Execute the handler's start method synchronously
            auto result = run_task_sync(
                handler->start_async(std::move(start_ctx),
                                     std::any(input)));

            // Build the NexusTaskCompletion from the result
            coresdk::nexus::NexusTaskCompletion completion;
            completion.set_task_token(
                std::string(task_token.begin(), task_token.end()));

            auto* response = completion.mutable_completed();
            auto* start_response = response->mutable_start_operation();

            if (result.is_sync) {
                // Synchronous completion -- set the payload
                auto* sync = start_response->mutable_sync_success();
                // The sync_result is a std::any; if it holds raw bytes
                // (std::vector<uint8_t>), set them as the payload data.
                if (result.sync_result.has_value()) {
                    try {
                        auto& bytes = std::any_cast<
                            const std::vector<uint8_t>&>(
                            result.sync_result);
                        sync->mutable_payload()->set_data(
                            bytes.data(), bytes.size());
                    } catch (const std::bad_any_cast&) {
                        // If the result is a string, serialize as string
                        try {
                            auto& str = std::any_cast<const std::string&>(
                                result.sync_result);
                            sync->mutable_payload()->set_data(
                                str.data(), str.size());
                        } catch (const std::bad_any_cast&) {
                            // Other types: leave payload empty
                        }
                    }
                }
            } else {
                // Async completion -- set the operation token
                auto* async_resp = start_response->mutable_async_success();
                async_resp->set_operation_token(
                    result.async_operation_token);
            }

            // NOTE: Outbound links are not yet supported by the
            // StartOperationResponse proto. When the proto adds a links
            // field, populate it from start_ctx.outbound_links here.

            std::string bytes;
            completion.SerializeToString(&bytes);
            std::vector<uint8_t> completion_bytes(
                bytes.begin(), bytes.end());
            if (bridge_worker) {
                bridge_worker->complete_nexus_task_async(
                    std::span<const uint8_t>(completion_bytes),
                    [](std::string) {});
            }
        } catch (const nexus::OperationException& e) {
            // Handler threw a typed Nexus error -- send with proper error type
            send_nexus_error_completion(
                bridge_worker, task_token,
                e.error_type_string(), e.what());
        } catch (const std::exception& e) {
            // Handler threw a generic exception -- send INTERNAL error
            send_nexus_error_completion(
                bridge_worker, task_token,
                "INTERNAL", e.what());
        }

        // Remove from running tasks
        {
            std::lock_guard lock(running_tasks_mutex_);
            running_tasks_.erase(token_key);
        }
    });
}

void NexusWorker::handle_cancel_task(
    const std::vector<uint8_t>& task_token) {
    std::string token_key(task_token.begin(), task_token.end());
    std::lock_guard lock(running_tasks_mutex_);
    auto it = running_tasks_.find(token_key);
    if (it != running_tasks_.end()) {
        it->second->cancel_source.request_stop();
    }
}

nexus::NexusServiceDefinition* NexusWorker::find_service(
    const std::string& name) const {
    auto it = services_by_name_.find(name);
    if (it != services_by_name_.end()) {
        return it->second.get();
    }
    return nullptr;
}

}  // namespace temporalio::worker::internal
