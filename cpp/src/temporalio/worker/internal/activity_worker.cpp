#include "temporalio/worker/internal/activity_worker.h"

#include <algorithm>
#include <chrono>
#include <mutex>
#include <stdexcept>
#include <thread>
#include <utility>

#include <google/protobuf/duration.pb.h>
#include <google/protobuf/timestamp.pb.h>
#include <temporal/sdk/core/activity_task/activity_task.pb.h>
#include <temporal/sdk/core/activity_result/activity_result.pb.h>
#include <temporal/sdk/core/core_interface.pb.h>
#include <temporal/api/common/v1/message.pb.h>
#include <temporal/api/failure/v1/message.pb.h>

#include "temporalio/coro/run_sync.h"
#include "temporalio/bridge/worker.h"
#include "temporalio/converters/data_converter.h"
#include "temporalio/converters/payload_conversion.h"
#include "temporalio/exceptions/temporal_exception.h"
#include "temporalio/worker/interceptors/worker_interceptor.h"

namespace {

/// Convert protobuf Timestamp to system_clock::time_point.
std::chrono::system_clock::time_point to_time_point(
    const google::protobuf::Timestamp& ts) {
    auto duration = std::chrono::seconds(ts.seconds()) +
                    std::chrono::nanoseconds(ts.nanos());
    return std::chrono::system_clock::time_point(
        std::chrono::duration_cast<std::chrono::system_clock::duration>(
            duration));
}

/// Convert protobuf Duration to std::chrono::milliseconds.
std::chrono::milliseconds to_millis(const google::protobuf::Duration& d) {
    return std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::seconds(d.seconds()) +
        std::chrono::nanoseconds(d.nanos()));
}

// Bring the shared run_task_sync into this translation unit's anonymous
// namespace so existing call sites (e.g., execute_activity) work unchanged.
using temporalio::coro::run_task_sync;

}  // namespace

namespace temporalio::worker::internal {

ActivityWorker::ActivityWorker(ActivityWorkerOptions options)
    : options_(std::move(options)) {}

ActivityWorker::~ActivityWorker() {
    // Stop and join the shutdown timer thread first, since it accesses
    // running_activities_mutex_ and running_activities_.
    // jthread destructor requests stop and joins automatically, but we
    // do it explicitly here to ensure ordering before activity joins.
    if (shutdown_timer_thread_.joinable()) {
        shutdown_timer_thread_.request_stop();
        shutdown_timer_thread_.join();
    }

    // Now join all activity threads before destroying members they reference.
    std::vector<std::shared_ptr<RunningActivity>> to_join;
    {
        std::lock_guard lock(running_activities_mutex_);
        for (auto& [_, running] : running_activities_) {
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
ActivityWorker::poll_activity_task() {
    if (!options_.bridge_worker) {
        co_return std::nullopt;
    }

    auto tcs = std::make_shared<
        coro::TaskCompletionSource<std::optional<std::vector<uint8_t>>>>();

    options_.bridge_worker->poll_activity_task_async(
        [tcs](std::optional<std::vector<uint8_t>> result,
              std::string error) {
            if (!error.empty()) {
                tcs->try_set_exception(std::make_exception_ptr(
                    std::runtime_error(
                        "Failed polling activity task: " + error)));
            } else {
                tcs->try_set_result(std::move(result));
            }
        });

    co_return co_await tcs->task();
}

coro::Task<void> ActivityWorker::complete_activity_task(
    const std::vector<uint8_t>& completion_bytes) {
    if (!options_.bridge_worker) {
        co_return;
    }

    auto tcs = std::make_shared<coro::TaskCompletionSource<void>>();

    options_.bridge_worker->complete_activity_task_async(
        std::span<const uint8_t>(completion_bytes),
        [tcs](std::string error) {
            if (error.empty()) {
                tcs->try_set_result();
            } else {
                tcs->try_set_exception(std::make_exception_ptr(
                    std::runtime_error(
                        "Failed completing activity task: " + error)));
            }
        });

    co_await tcs->task();
}

coro::Task<void> ActivityWorker::execute_async() {
    // Poll loop: continuously poll for activity tasks from the bridge.
    // Each task is either a Start (new activity) or Cancel (cancel running).
    //
    // The flow mirrors C# ActivityWorker.ExecuteAsync():
    // 1. Poll for activity task (blocks until one available or shutdown)
    // 2. For Start tasks: look up ActivityDefinition, create context, dispatch
    // 3. For Cancel tasks: cancel the running activity's stop token
    // 4. Null result means shutdown -- exit loop
    // 5. Wait for all running activities to complete

    while (true) {
        auto task_result = co_await poll_activity_task();

        // Null result means the poller has shut down
        if (!task_result.has_value()) {
            break;
        }

        try {
            start_activity(task_result.value());
        } catch (const std::exception& e) {
            // Failed to start the activity. Try to extract the task token
            // and send a failure completion.
            coresdk::activity_task::ActivityTask task;
            if (task.ParseFromArray(task_result->data(),
                                     static_cast<int>(task_result->size()))) {
                coresdk::ActivityTaskCompletion completion;
                completion.set_task_token(task.task_token());
                auto* failure = completion.mutable_result()
                                    ->mutable_failed()
                                    ->mutable_failure();
                failure->set_message(
                    std::string("Failed starting activity task: ") + e.what());
                failure->set_source("CppSDK");
                std::string bytes;
                completion.SerializeToString(&bytes);
                std::vector<uint8_t> completion_bytes(bytes.begin(),
                                                      bytes.end());
                if (options_.bridge_worker) {
                    options_.bridge_worker->complete_activity_task_async(
                        std::span<const uint8_t>(completion_bytes),
                        [](std::string) {});
                }
            }
        }
    }

    // Wait for all running activity threads to complete after shutdown.
    // Use a TCS-based co_await instead of thread.join() to avoid deadlock:
    // this coroutine runs on a detached poll thread, and joining an activity
    // thread from the poll thread can self-join on Linux (EDEADLK).
    //
    // Hold the mutex while creating the TCS and checking the map to
    // eliminate TOCTOU races with execute_activity() finishing concurrently.
    {
        std::lock_guard lock(running_activities_mutex_);
        if (!running_activities_.empty()) {
            all_activities_done_tcs_ =
                std::make_shared<coro::TaskCompletionSource<void>>();
        }
    }
    if (all_activities_done_tcs_) {
        co_await all_activities_done_tcs_->task();
    }
}

void ActivityWorker::start_activity(
    const std::vector<uint8_t>& task_bytes) {
    // Deserialize the protobuf ActivityTask from the bridge bytes.
    coresdk::activity_task::ActivityTask task;
    if (!task.ParseFromArray(task_bytes.data(),
                             static_cast<int>(task_bytes.size()))) {
        throw std::runtime_error(
            "Failed to deserialize ActivityTask protobuf");
    }

    // Extract task token
    const auto& token_str = task.task_token();
    std::vector<uint8_t> task_token(token_str.begin(), token_str.end());

    // Handle Cancel variant
    if (task.has_cancel()) {
        cancel_activity(task_token);
        return;
    }

    // Must be a Start variant
    if (!task.has_start()) {
        throw std::runtime_error(
            "ActivityTask has neither start nor cancel variant");
    }

    // Prevent launching new activities after shutdown has been requested.
    if (shutdown_requested_.load(std::memory_order_acquire)) {
        throw std::runtime_error(
            "Activity worker is shutting down, cannot start new activity");
    }

    const auto& start = task.start();
    std::string activity_type = start.activity_type();

    // Look up the activity definition
    auto it = options_.activities.find(activity_type);
    std::shared_ptr<activities::ActivityDefinition> defn;
    if (it != options_.activities.end()) {
        defn = it->second;
    } else if (options_.dynamic_activity) {
        defn = options_.dynamic_activity;
    } else {
        throw std::runtime_error(
            "Activity " + activity_type +
            " is not registered on this worker");
    }

    // Create the running activity state
    auto running = std::make_shared<RunningActivity>();
    running->task_token = task_token;

    // Populate full ActivityInfo from the protobuf Start message.
    activities::ActivityInfo info;
    info.activity_id = start.activity_id();
    info.activity_type = activity_type;
    info.task_queue = options_.task_queue;
    info.namespace_ = options_.ns;
    info.task_token = task_token;
    info.attempt = static_cast<int>(start.attempt());
    info.is_local = start.is_local();

    if (start.has_scheduled_time()) {
        info.scheduled_time = to_time_point(start.scheduled_time());
    }
    if (start.has_current_attempt_scheduled_time()) {
        info.current_attempt_scheduled_time =
            to_time_point(start.current_attempt_scheduled_time());
    }
    if (start.has_started_time()) {
        info.started_time = to_time_point(start.started_time());
    }
    if (start.has_schedule_to_close_timeout()) {
        info.schedule_to_close_timeout =
            to_millis(start.schedule_to_close_timeout());
    }
    if (start.has_start_to_close_timeout()) {
        info.start_to_close_timeout =
            to_millis(start.start_to_close_timeout());
    }
    if (start.has_heartbeat_timeout()) {
        info.heartbeat_timeout = to_millis(start.heartbeat_timeout());
    }

    // Populate workflow info if this activity was started by a workflow.
    if (start.has_workflow_execution()) {
        info.workflow_id = start.workflow_execution().workflow_id();
        info.workflow_run_id = start.workflow_execution().run_id();
    }
    if (!start.workflow_namespace().empty()) {
        info.workflow_namespace = start.workflow_namespace();
    }
    if (!start.workflow_type().empty()) {
        info.workflow_type = start.workflow_type();
    }

    running->context = std::make_shared<activities::ActivityExecutionContext>(
        std::move(info),
        running->cancel_source.get_token(),
        shutdown_source_.get_token());

    // Set up heartbeat callback.
    auto bridge_worker = options_.bridge_worker;
    auto data_converter = options_.data_converter;
    running->context->set_heartbeat_callback(
        [bridge_worker, task_token, data_converter](const std::any& details) {
            coresdk::ActivityHeartbeat heartbeat;
            heartbeat.set_task_token(
                std::string(task_token.begin(), task_token.end()));

            // Serialize heartbeat details via data converter
            if (details.has_value() && data_converter &&
                data_converter->payload_converter) {
                try {
                    auto payload =
                        data_converter->payload_converter->to_payload(details);
                    *heartbeat.add_details() =
                        converters::to_proto_payload(payload);
                } catch (...) {
                    // Heartbeat is best-effort; swallow serialization errors
                }
            }

            std::string bytes;
            heartbeat.SerializeToString(&bytes);
            std::vector<uint8_t> heartbeat_bytes(bytes.begin(), bytes.end());
            bridge_worker->record_activity_heartbeat(
                std::span<const uint8_t>(heartbeat_bytes));
        });

    // Extract input arguments from the protobuf Start message.
    // Convert each protobuf Payload to a converters::Payload (preserving
    // encoding metadata). The typed activity handlers will use
    // decode_payload_value to properly decode to the target type.
    std::vector<std::any> input_args;
    input_args.reserve(static_cast<size_t>(start.input_size()));
    for (const auto& proto_payload : start.input()) {
        input_args.push_back(
            std::any(converters::from_proto_payload(proto_payload)));
    }

    // Track the running activity
    {
        std::string token_key(task_token.begin(), task_token.end());
        std::lock_guard lock(running_activities_mutex_);
        running_activities_[token_key] = running;
    }

    // Execute the activity on a separate thread.
    // Track the active count so execute_async() can co_await completion
    // without calling thread.join() (which would deadlock if called from
    // the same poll thread that spawned the activity thread on Linux).
    auto token_key = std::string(task_token.begin(), task_token.end());

    running->thread = std::jthread([this, running, defn, task_token,
                                    token_key,
                                    args = std::move(input_args)]() {
        execute_activity(running, defn, task_token, token_key, args);
    });
}

void ActivityWorker::cancel_activity(
    const std::vector<uint8_t>& task_token) {
    std::string token_key(task_token.begin(), task_token.end());
    std::lock_guard lock(running_activities_mutex_);
    auto it = running_activities_.find(token_key);
    if (it != running_activities_.end()) {
        auto& running = it->second;
        {
            std::lock_guard act_lock(running->mutex);
            running->server_requested_cancel = true;
        }
        running->cancel_source.request_stop();
    }
}

void ActivityWorker::execute_activity(
    std::shared_ptr<RunningActivity> running,
    std::shared_ptr<activities::ActivityDefinition> defn,
    std::vector<uint8_t> task_token,
    std::string token_key,
    const std::vector<std::any>& args) {
    // Set the activity context for this thread
    activities::ActivityContextScope scope(*running->context);

    // Build the completion
    coresdk::ActivityTaskCompletion completion;
    completion.set_task_token(
        std::string(task_token.begin(), task_token.end()));

    try {
        // Execute the activity via its definition.
        // In the full implementation, this would go through the
        // interceptor chain. For now, invoke directly.
        auto result = run_task_sync(defn->execute(args));

        // Activity completed successfully
        auto* completed = completion.mutable_result()->mutable_completed();
        // Serialize result via data converter
        if (result.has_value() && options_.data_converter &&
            options_.data_converter->payload_converter) {
            auto conv_payload =
                options_.data_converter->payload_converter->to_payload(result);
            *completed->mutable_result() =
                converters::to_proto_payload(conv_payload);
        }

    } catch (const activities::CompleteAsyncException&) {
        // Activity will complete asynchronously
        completion.mutable_result()->mutable_will_complete_async();
    } catch (const std::exception& e) {
        // Activity failed
        auto* failure = completion.mutable_result()
                            ->mutable_failed()
                            ->mutable_failure();
        failure->set_message(e.what());
        failure->set_source("CppSDK");
    }

    // Send completion to the bridge
    std::string bytes;
    completion.SerializeToString(&bytes);
    std::vector<uint8_t> completion_bytes(bytes.begin(), bytes.end());
    if (options_.bridge_worker) {
        options_.bridge_worker->complete_activity_task_async(
            std::span<const uint8_t>(completion_bytes),
            [](std::string) {});
    }

    // Mark done and remove from running activities.
    // If this was the last running activity and execute_async() is waiting,
    // signal the TCS to unblock it. Both the map check and TCS signal are
    // under the mutex to synchronize with execute_async()'s check.
    //
    // Detach our own thread BEFORE erasing from the map. After the erase,
    // this lambda's captured shared_ptr<RunningActivity> may be the last
    // reference. When the lambda finishes and the capture is destroyed,
    // ~RunningActivity() would call ~jthread() which attempts to join the
    // current thread. On Linux, pthread_join(self) returns EDEADLK (abort).
    // Detaching makes ~jthread() a no-op (joinable() returns false).
    // The destructor's join loop already guards with joinable().
    if (running->thread.joinable()) {
        running->thread.detach();
    }

    running->mark_done();
    {
        std::lock_guard lock(running_activities_mutex_);
        running_activities_.erase(token_key);
        if (running_activities_.empty() && all_activities_done_tcs_) {
            all_activities_done_tcs_->try_set_result();
        }
    }
}

void ActivityWorker::notify_shutdown() {
    shutdown_requested_.store(true, std::memory_order_release);
    shutdown_source_.request_stop();

    // If there's a graceful shutdown timeout, schedule cancellation
    // after that period. Otherwise, cancel immediately.
    if (options_.graceful_shutdown_timeout.count() > 0) {
        // Use the member jthread so the timer is joined in the destructor,
        // preventing dangling pointer access to our members.
        // The stop_token allows early termination if the worker is destroyed
        // before the timeout elapses.
        auto timeout = options_.graceful_shutdown_timeout;
        shutdown_timer_thread_ = std::jthread(
            [this, timeout](std::stop_token stop_token) {
                // Sleep in small increments to allow early stop
                auto deadline = std::chrono::steady_clock::now() + timeout;
                while (std::chrono::steady_clock::now() < deadline) {
                    if (stop_token.stop_requested()) {
                        return;
                    }
                    std::this_thread::sleep_for(std::chrono::milliseconds(50));
                }
                if (stop_token.stop_requested()) {
                    return;
                }
                std::lock_guard lock(running_activities_mutex_);
                for (auto& [_, running] : running_activities_) {
                    running->cancel_source.request_stop();
                }
            });
    } else {
        // No grace period -- cancel all immediately
        std::lock_guard lock(running_activities_mutex_);
        for (auto& [_, running] : running_activities_) {
            running->cancel_source.request_stop();
        }
    }
}

}  // namespace temporalio::worker::internal
