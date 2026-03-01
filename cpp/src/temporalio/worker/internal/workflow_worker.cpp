#include "temporalio/worker/internal/workflow_worker.h"

#include <chrono>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

#include <google/protobuf/duration.pb.h>
#include <google/protobuf/empty.pb.h>
#include <google/protobuf/timestamp.pb.h>
#include <temporal/api/common/v1/message.pb.h>
#include <temporal/api/failure/v1/message.pb.h>
#include <temporal/sdk/core/activity_result/activity_result.pb.h>
#include <temporal/sdk/core/child_workflow/child_workflow.pb.h>
#include <temporal/sdk/core/workflow_activation/workflow_activation.pb.h>
#include <temporal/sdk/core/workflow_commands/workflow_commands.pb.h>
#include <temporal/sdk/core/workflow_completion/workflow_completion.pb.h>

#include "temporalio/bridge/worker.h"
#include "temporalio/converters/data_converter.h"
#include "temporalio/converters/payload_conversion.h"
#include "temporalio/exceptions/temporal_exception.h"

namespace temporalio::worker::internal {

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

/// Set protobuf Duration from std::chrono::milliseconds.
void set_duration_millis(google::protobuf::Duration* d,
                         std::chrono::milliseconds ms) {
    auto secs = std::chrono::duration_cast<std::chrono::seconds>(ms);
    auto nanos = std::chrono::duration_cast<std::chrono::nanoseconds>(
        ms - secs);
    d->set_seconds(secs.count());
    d->set_nanos(static_cast<int32_t>(nanos.count()));
}

/// Convert a protobuf Payload to a converters::Payload wrapped in std::any.
/// Preserves the encoding metadata so that downstream typed handlers can
/// use the DataConverter to properly decode to the target type.
std::any payload_to_any(const temporal::api::common::v1::Payload& payload) {
    return std::any(converters::from_proto_payload(payload));
}

/// Convert protobuf activity result to WorkflowInstance::ActivityResolution.
WorkflowInstance::ActivityResolution convert_activity_resolution(
    uint32_t seq,
    const coresdk::activity_result::ActivityResolution& proto_res) {
    WorkflowInstance::ActivityResolution res;
    res.seq = seq;
    if (proto_res.has_completed()) {
        res.status = WorkflowInstance::ResolutionStatus::kCompleted;
        if (proto_res.completed().has_result()) {
            res.result = payload_to_any(proto_res.completed().result());
        }
    } else if (proto_res.has_failed()) {
        res.status = WorkflowInstance::ResolutionStatus::kFailed;
        res.failure = proto_res.failed().failure().message();
    } else if (proto_res.has_cancelled()) {
        res.status = WorkflowInstance::ResolutionStatus::kCancelled;
        res.failure = proto_res.cancelled().failure().message();
    }
    return res;
}

/// Convert protobuf child workflow result to
/// WorkflowInstance::ChildWorkflowResolution.
WorkflowInstance::ChildWorkflowResolution convert_child_workflow_resolution(
    uint32_t seq,
    const coresdk::child_workflow::ChildWorkflowResult& proto_res) {
    WorkflowInstance::ChildWorkflowResolution res;
    res.seq = seq;
    if (proto_res.has_completed()) {
        res.status = WorkflowInstance::ResolutionStatus::kCompleted;
        if (proto_res.completed().has_result()) {
            res.result = payload_to_any(proto_res.completed().result());
        }
    } else if (proto_res.has_failed()) {
        res.status = WorkflowInstance::ResolutionStatus::kFailed;
        res.failure = proto_res.failed().failure().message();
    } else if (proto_res.has_cancelled()) {
        res.status = WorkflowInstance::ResolutionStatus::kCancelled;
        res.failure = proto_res.cancelled().failure().message();
    }
    return res;
}

/// Convert protobuf WorkflowActivation jobs to WorkflowInstance::Job structs.
std::vector<WorkflowInstance::Job> convert_jobs(
    const coresdk::workflow_activation::WorkflowActivation& activation) {
    std::vector<WorkflowInstance::Job> jobs;
    jobs.reserve(static_cast<size_t>(activation.jobs_size()));

    for (const auto& proto_job : activation.jobs()) {
        WorkflowInstance::Job job;

        if (proto_job.has_initialize_workflow()) {
            job.type = WorkflowInstance::JobType::kStartWorkflow;
            // Extract workflow arguments as std::any-wrapped Payloads for
            // the workflow run function. Instance creation metadata is
            // handled separately via create_instance().
            const auto& init = proto_job.initialize_workflow();
            std::vector<std::any> args;
            args.reserve(static_cast<size_t>(init.arguments_size()));
            for (const auto& payload : init.arguments()) {
                args.push_back(payload_to_any(payload));
            }
            job.data = std::any(std::move(args));
        } else if (proto_job.has_fire_timer()) {
            job.type = WorkflowInstance::JobType::kFireTimer;
            job.data = std::any(proto_job.fire_timer().seq());
        } else if (proto_job.has_resolve_activity()) {
            job.type = WorkflowInstance::JobType::kResolveActivity;
            const auto& ra = proto_job.resolve_activity();
            job.data = std::any(convert_activity_resolution(
                ra.seq(), ra.result()));
        } else if (proto_job.has_resolve_child_workflow_execution()) {
            job.type = WorkflowInstance::JobType::kResolveChildWorkflow;
            const auto& rc = proto_job.resolve_child_workflow_execution();
            job.data = std::any(convert_child_workflow_resolution(
                rc.seq(), rc.result()));
        } else if (proto_job.has_signal_workflow()) {
            job.type = WorkflowInstance::JobType::kSignalWorkflow;
            const auto& sig = proto_job.signal_workflow();
            std::vector<std::any> args;
            args.reserve(static_cast<size_t>(sig.input_size()));
            for (const auto& payload : sig.input()) {
                args.push_back(payload_to_any(payload));
            }
            job.data = std::any(std::make_pair(
                std::string(sig.signal_name()), std::move(args)));
        } else if (proto_job.has_query_workflow()) {
            job.type = WorkflowInstance::JobType::kQueryWorkflow;
            const auto& q = proto_job.query_workflow();
            WorkflowInstance::QueryWorkflowData qd;
            qd.query_id = q.query_id();
            qd.query_name = q.query_type();
            qd.args.reserve(static_cast<size_t>(q.arguments_size()));
            for (const auto& payload : q.arguments()) {
                qd.args.push_back(payload_to_any(payload));
            }
            job.data = std::any(std::move(qd));
        } else if (proto_job.has_cancel_workflow()) {
            job.type = WorkflowInstance::JobType::kCancelWorkflow;
            job.data = std::any(proto_job.cancel_workflow().reason());
        } else if (proto_job.has_do_update()) {
            job.type = WorkflowInstance::JobType::kDoUpdate;
            const auto& u = proto_job.do_update();
            WorkflowInstance::DoUpdateData ud;
            ud.id = u.id();
            ud.name = u.name();
            ud.protocol_instance_id = u.protocol_instance_id();
            ud.run_validator = u.run_validator();
            ud.args.reserve(static_cast<size_t>(u.input_size()));
            for (const auto& payload : u.input()) {
                ud.args.push_back(payload_to_any(payload));
            }
            job.data = std::any(std::move(ud));
        } else if (proto_job.has_notify_has_patch()) {
            job.type = WorkflowInstance::JobType::kNotifyHasPatch;
            job.data = std::any(
                std::string(proto_job.notify_has_patch().patch_id()));
        } else if (proto_job.has_resolve_signal_external_workflow()) {
            job.type =
                WorkflowInstance::JobType::kResolveSignalExternalWorkflow;
            job.data = std::any(
                proto_job.resolve_signal_external_workflow().seq());
        } else if (proto_job.has_resolve_request_cancel_external_workflow()) {
            job.type = WorkflowInstance::JobType::
                kResolveRequestCancelExternalWorkflow;
            job.data = std::any(
                proto_job.resolve_request_cancel_external_workflow().seq());
        } else if (proto_job.has_resolve_nexus_operation()) {
            job.type = WorkflowInstance::JobType::kResolveNexusOperation;
            job.data = std::any(
                proto_job.resolve_nexus_operation().seq());
        } else if (proto_job.has_update_random_seed()) {
            // Random seed updates are applied to the instance directly.
            // We handle this as a start workflow job type (ignored if already
            // started) since WorkflowInstance doesn't have a dedicated type.
            // The seed is applied during create_instance from InitializeWorkflow.
            continue;
        } else if (proto_job.has_remove_from_cache()) {
            // Evictions are handled separately before this function is called.
            continue;
        } else {
            // Unknown job type -- skip.
            continue;
        }

        jobs.push_back(std::move(job));
    }

    return jobs;
}

/// Convert WorkflowInstance commands to protobuf WorkflowCommand messages.
void convert_commands_to_proto(
    const std::vector<WorkflowInstance::Command>& commands,
    coresdk::workflow_completion::Success* success,
    const std::shared_ptr<converters::DataConverter>& data_converter) {
    for (const auto& cmd : commands) {
        auto* proto_cmd = success->add_commands();

        switch (cmd.type) {
            case WorkflowInstance::CommandType::kStartTimer: {
                auto data = std::any_cast<WorkflowInstance::StartTimerData>(
                    cmd.data);
                auto* st = proto_cmd->mutable_start_timer();
                st->set_seq(data.seq);
                set_duration_millis(st->mutable_start_to_fire_timeout(),
                                   data.duration);
                break;
            }
            case WorkflowInstance::CommandType::kCancelTimer: {
                auto data = std::any_cast<WorkflowInstance::CancelTimerData>(
                    cmd.data);
                auto* ct = proto_cmd->mutable_cancel_timer();
                ct->set_seq(data.seq);
                break;
            }
            case WorkflowInstance::CommandType::kCompleteWorkflow: {
                auto* cwe = proto_cmd->mutable_complete_workflow_execution();
                // cmd.data holds the workflow return value (std::any)
                if (cmd.data.has_value() && data_converter &&
                    data_converter->payload_converter) {
                    try {
                        auto payload =
                            data_converter->payload_converter->to_payload(
                                cmd.data);
                        *cwe->mutable_result() =
                            converters::to_proto_payload(payload);
                    } catch (...) {
                        // If serialization fails, leave result empty
                        // (void workflow)
                    }
                }
                break;
            }
            case WorkflowInstance::CommandType::kFailWorkflow: {
                auto msg = std::any_cast<std::string>(cmd.data);
                auto* fw = proto_cmd->mutable_fail_workflow_execution();
                fw->mutable_failure()->set_message(msg);
                break;
            }
            case WorkflowInstance::CommandType::kCancelWorkflow: {
                proto_cmd->mutable_cancel_workflow_execution();
                break;
            }
            case WorkflowInstance::CommandType::kContinueAsNew: {
                proto_cmd->mutable_continue_as_new_workflow_execution();
                break;
            }
            case WorkflowInstance::CommandType::kSetPatchMarker: {
                auto patch_id = std::any_cast<std::string>(cmd.data);
                auto* sp = proto_cmd->mutable_set_patch_marker();
                sp->set_patch_id(patch_id);
                sp->set_deprecated(false);
                break;
            }
            case WorkflowInstance::CommandType::kRespondQuery: {
                auto data = std::any_cast<
                    WorkflowInstance::QueryResponseData>(cmd.data);
                auto* qr = proto_cmd->mutable_respond_to_query();
                qr->set_query_id(data.query_id);
                auto* succeeded = qr->mutable_succeeded();
                if (data.result.has_value() && data_converter &&
                    data_converter->payload_converter) {
                    try {
                        auto payload =
                            data_converter->payload_converter->to_payload(
                                data.result);
                        *succeeded->mutable_response() =
                            converters::to_proto_payload(payload);
                    } catch (...) {
                    }
                }
                break;
            }
            case WorkflowInstance::CommandType::kRespondQueryFailed: {
                auto data = std::any_cast<
                    WorkflowInstance::QueryResponseData>(cmd.data);
                auto* qr = proto_cmd->mutable_respond_to_query();
                qr->set_query_id(data.query_id);
                qr->mutable_failed()->set_message(data.error);
                break;
            }
            case WorkflowInstance::CommandType::kUpdateAccepted: {
                auto data = std::any_cast<
                    WorkflowInstance::UpdateResponseData>(cmd.data);
                auto* ur = proto_cmd->mutable_update_response();
                ur->set_protocol_instance_id(data.protocol_instance_id);
                ur->mutable_accepted();
                break;
            }
            case WorkflowInstance::CommandType::kUpdateRejected: {
                auto data = std::any_cast<
                    WorkflowInstance::UpdateResponseData>(cmd.data);
                auto* ur = proto_cmd->mutable_update_response();
                ur->set_protocol_instance_id(data.protocol_instance_id);
                ur->mutable_rejected()->set_message(data.error);
                break;
            }
            case WorkflowInstance::CommandType::kUpdateCompleted: {
                auto data = std::any_cast<
                    WorkflowInstance::UpdateResponseData>(cmd.data);
                auto* ur = proto_cmd->mutable_update_response();
                ur->set_protocol_instance_id(data.protocol_instance_id);
                auto* completed = ur->mutable_completed();
                if (data.result.has_value() && data_converter &&
                    data_converter->payload_converter) {
                    try {
                        auto payload =
                            data_converter->payload_converter->to_payload(
                                data.result);
                        *completed =
                            converters::to_proto_payload(payload);
                    } catch (...) {
                    }
                }
                break;
            }
            case WorkflowInstance::CommandType::kScheduleActivity: {
                auto data = std::any_cast<
                    WorkflowInstance::ScheduleActivityData>(cmd.data);
                auto* sa = proto_cmd->mutable_schedule_activity();
                sa->set_seq(data.seq);
                sa->set_activity_type(data.activity_type);
                sa->set_task_queue(data.task_queue);
                // ActivityId is required by the server. Use the user-provided
                // ID or auto-generate one from the sequence number.
                sa->set_activity_id(
                    data.activity_id.value_or(std::to_string(data.seq)));
                for (const auto& arg : data.args) {
                    if (arg.has_value() && data_converter &&
                        data_converter->payload_converter) {
                        try {
                            auto payload =
                                data_converter->payload_converter->to_payload(
                                    arg);
                            *sa->add_arguments() =
                                converters::to_proto_payload(payload);
                        } catch (...) {
                            sa->add_arguments();  // empty placeholder on failure
                        }
                    } else {
                        sa->add_arguments();
                    }
                }
                if (data.schedule_to_close_timeout.has_value()) {
                    set_duration_millis(
                        sa->mutable_schedule_to_close_timeout(),
                        *data.schedule_to_close_timeout);
                }
                if (data.schedule_to_start_timeout.has_value()) {
                    set_duration_millis(
                        sa->mutable_schedule_to_start_timeout(),
                        *data.schedule_to_start_timeout);
                }
                if (data.start_to_close_timeout.has_value()) {
                    set_duration_millis(
                        sa->mutable_start_to_close_timeout(),
                        *data.start_to_close_timeout);
                }
                if (data.heartbeat_timeout.has_value()) {
                    set_duration_millis(
                        sa->mutable_heartbeat_timeout(),
                        *data.heartbeat_timeout);
                }
                if (data.retry_policy.has_value()) {
                    auto* rp = sa->mutable_retry_policy();
                    set_duration_millis(
                        rp->mutable_initial_interval(),
                        data.retry_policy->initial_interval);
                    rp->set_backoff_coefficient(
                        data.retry_policy->backoff_coefficient);
                    if (data.retry_policy->maximum_interval.has_value()) {
                        set_duration_millis(
                            rp->mutable_maximum_interval(),
                            *data.retry_policy->maximum_interval);
                    }
                    rp->set_maximum_attempts(
                        data.retry_policy->maximum_attempts);
                    for (const auto& err_type :
                         data.retry_policy->non_retryable_error_types) {
                        rp->add_non_retryable_error_types(err_type);
                    }
                }
                sa->set_cancellation_type(
                    static_cast<coresdk::workflow_commands::
                        ActivityCancellationType>(
                        static_cast<int>(data.cancellation_type)));
                break;
            }
            case WorkflowInstance::CommandType::kScheduleLocalActivity: {
                auto* sla = proto_cmd->mutable_schedule_local_activity();
                sla->set_seq(cmd.seq);
                break;
            }
            case WorkflowInstance::CommandType::kRequestCancelActivity: {
                auto data = std::any_cast<
                    WorkflowInstance::RequestCancelActivityData>(cmd.data);
                auto* rca = proto_cmd->mutable_request_cancel_activity();
                rca->set_seq(data.seq);
                break;
            }
            case WorkflowInstance::CommandType::kStartChildWorkflow: {
                auto* scw =
                    proto_cmd->mutable_start_child_workflow_execution();
                scw->set_seq(cmd.seq);
                break;
            }
            case WorkflowInstance::CommandType::kCancelChildWorkflow: {
                auto* ccw =
                    proto_cmd->mutable_cancel_child_workflow_execution();
                ccw->set_child_workflow_seq(cmd.seq);
                break;
            }
            case WorkflowInstance::CommandType::kSignalExternalWorkflow: {
                auto* sew =
                    proto_cmd->mutable_signal_external_workflow_execution();
                sew->set_seq(cmd.seq);
                break;
            }
            case WorkflowInstance::CommandType::kCancelExternalWorkflow: {
                auto* rcew = proto_cmd
                    ->mutable_request_cancel_external_workflow_execution();
                rcew->set_seq(cmd.seq);
                break;
            }
            case WorkflowInstance::CommandType::kScheduleNexusOperation: {
                auto* sno = proto_cmd->mutable_schedule_nexus_operation();
                sno->set_seq(cmd.seq);
                break;
            }
            case WorkflowInstance::CommandType::kCancelNexusOperation: {
                auto* cno =
                    proto_cmd->mutable_request_cancel_nexus_operation();
                cno->set_seq(cmd.seq);
                break;
            }
        }
    }
}

}  // namespace

WorkflowWorker::WorkflowWorker(WorkflowWorkerOptions options)
    : options_(std::move(options)) {
    if (options_.debug_mode) {
        // In debug mode, disable deadlock detection by using a very
        // large timeout (effectively infinite).
        deadlock_timeout_ = std::chrono::seconds(3600);
    }
}

WorkflowWorker::~WorkflowWorker() = default;

coro::Task<std::optional<std::vector<uint8_t>>>
WorkflowWorker::poll_activation() {
    if (!options_.bridge_worker) {
        // No bridge worker available -- return shutdown signal
        co_return std::nullopt;
    }

    auto tcs = std::make_shared<
        coro::TaskCompletionSource<std::optional<std::vector<uint8_t>>>>();

    options_.bridge_worker->poll_workflow_activation_async(
        [tcs](std::optional<std::vector<uint8_t>> result,
              std::string error) {
            if (!error.empty()) {
                tcs->try_set_exception(std::make_exception_ptr(
                    std::runtime_error(
                        "Failed polling workflow activation: " + error)));
            } else {
                tcs->try_set_result(std::move(result));
            }
        });

    co_return co_await tcs->task();
}

coro::Task<void> WorkflowWorker::complete_activation(
    const std::vector<uint8_t>& completion_bytes) {
    if (!options_.bridge_worker) {
        co_return;
    }

    auto tcs = std::make_shared<coro::TaskCompletionSource<void>>();

    options_.bridge_worker->complete_workflow_activation_async(
        std::span<const uint8_t>(completion_bytes),
        [tcs](std::string error) {
            if (error.empty()) {
                tcs->try_set_result();
            } else {
                tcs->try_set_exception(std::make_exception_ptr(
                    std::runtime_error(
                        "Failed completing workflow activation: " + error)));
            }
        });

    co_await tcs->task();
}

coro::Task<void> WorkflowWorker::execute_async() {
    // Poll loop: continuously poll for workflow activations from the bridge.
    // Each activation contains one or more jobs (start, signal, timer fire,
    // activity resolve, etc.) for a specific workflow run.
    //
    // The flow mirrors C# WorkflowWorker.ExecuteAsync():
    // 1. Poll for activation (blocks until one is available or shutdown)
    // 2. Look up or create WorkflowInstance by run ID
    // 3. Call instance.activate(jobs) to get commands
    // 4. Serialize commands as completion and send back to bridge
    // 5. Repeat until poll returns nullopt (shutdown)

    while (true) {
        // Poll for next activation
        auto activation_result = co_await poll_activation();

        // Null result means the poller has shut down
        if (!activation_result.has_value()) {
            break;
        }

        // Handle activation and send completion.
        // Errors during handling produce failure completions, never crash
        // the poll loop (matching C# behavior).
        co_await handle_activation(activation_result.value());
    }

    // Poll loop has ended (shutdown). With the current synchronous dispatch
    // model, there are no outstanding activations at this point.
}

coro::Task<void> WorkflowWorker::handle_activation(
    const std::vector<uint8_t>& activation_bytes) {
    // Deserialize the protobuf activation from the bridge bytes.
    coresdk::workflow_activation::WorkflowActivation activation;
    if (!activation.ParseFromArray(activation_bytes.data(),
                                   static_cast<int>(activation_bytes.size()))) {
        // Cannot parse activation -- build a failure completion with empty
        // run_id. This matches C# behavior where parse failure throws and
        // the outer catch builds a failure completion.
        coresdk::workflow_completion::WorkflowActivationCompletion
            fail_completion;
        fail_completion.set_run_id("");
        fail_completion.mutable_failed()->mutable_failure()->set_message(
            "Failed to parse workflow activation protobuf");
        std::string fail_bytes;
        fail_completion.SerializeToString(&fail_bytes);
        std::vector<uint8_t> fail_vec(fail_bytes.begin(), fail_bytes.end());
        co_await complete_activation(fail_vec);
        co_return;
    }

    const auto& run_id = activation.run_id();

    // Check for cache eviction (RemoveFromCache is always the sole job)
    if (activation.jobs_size() == 1 &&
        activation.jobs(0).has_remove_from_cache()) {
        const auto& eviction = activation.jobs(0).remove_from_cache();
        co_await handle_cache_eviction(run_id, eviction.message());
        co_return;
    }

    // Build the completion (success or failure)
    coresdk::workflow_completion::WorkflowActivationCompletion completion;
    completion.set_run_id(run_id);

    try {
        // Look up or create the workflow instance
        auto it = running_workflows_.find(run_id);
        if (it == running_workflows_.end()) {
            // Find the InitializeWorkflow job to get the workflow type
            // and full initialization data.
            const coresdk::workflow_activation::InitializeWorkflow* init_job =
                nullptr;
            std::string workflow_type;
            for (const auto& job : activation.jobs()) {
                if (job.has_initialize_workflow()) {
                    init_job = &job.initialize_workflow();
                    workflow_type = init_job->workflow_type();
                    break;
                }
            }

            // Use the full InitializeWorkflow overload if available,
            // otherwise fall back to the basic one.
            std::unique_ptr<WorkflowInstance> instance;
            if (init_job) {
                instance = create_instance(workflow_type, run_id, *init_job);
            } else {
                instance = create_instance(workflow_type, run_id);
            }

            if (!instance) {
                // Unknown workflow type -- send failure completion
                auto* failure = completion.mutable_failed();
                auto* fail_info = failure->mutable_failure();
                fail_info->set_message(
                    "Workflow type not registered: " + workflow_type);

                std::string bytes;
                completion.SerializeToString(&bytes);
                std::vector<uint8_t> completion_bytes(bytes.begin(),
                                                      bytes.end());
                try {
                    co_await complete_activation(completion_bytes);
                } catch (...) {
                    // Ignore completion errors (same as C#)
                }
                co_return;
            }
            auto [insert_it, inserted] = running_workflows_.emplace(
                run_id, std::move(instance));
            it = insert_it;
        }

        // Extract activation-level metadata from the protobuf envelope.
        WorkflowInstance::ActivationMetadata metadata;
        if (activation.has_timestamp()) {
            metadata.timestamp = to_time_point(activation.timestamp());
        }
        metadata.is_replaying = activation.is_replaying();
        metadata.history_length = static_cast<int>(activation.history_length());
        metadata.history_size_bytes = activation.history_size_bytes();
        metadata.continue_as_new_suggested =
            activation.continue_as_new_suggested();

        // Convert protobuf jobs to WorkflowInstance::Job structs.
        auto instance_jobs = convert_jobs(activation);

        // Activate the instance: process jobs and produce commands.
        auto commands =
            it->second->activate(instance_jobs, metadata);

        // Convert WorkflowInstance commands to protobuf and build the
        // successful completion.
        auto* success = completion.mutable_successful();
        convert_commands_to_proto(commands, success, options_.data_converter);

    } catch (const std::exception& e) {
        // Activation handling failure -- send failure completion
        completion.clear_successful();
        auto* failure = completion.mutable_failed();
        auto* fail_info = failure->mutable_failure();
        fail_info->set_message(
            std::string("Workflow activation failed: ") + e.what());
    }

    // Send completion to the bridge
    std::string bytes;
    completion.SerializeToString(&bytes);
    std::vector<uint8_t> completion_bytes(bytes.begin(), bytes.end());
    try {
        co_await complete_activation(completion_bytes);
    } catch (const std::exception& e) {
        // Log but don't propagate completion errors (same as C#)
        (void)e;
    }
}

coro::Task<void> WorkflowWorker::handle_cache_eviction(
    const std::string& run_id, const std::string& /*message*/) {
    running_workflows_.erase(run_id);

    // Send a successful empty completion for the eviction
    coresdk::workflow_completion::WorkflowActivationCompletion completion;
    completion.set_run_id(run_id);
    completion.mutable_successful();  // Empty success (no commands)

    std::string bytes;
    completion.SerializeToString(&bytes);
    std::vector<uint8_t> completion_bytes(bytes.begin(), bytes.end());
    try {
        co_await complete_activation(completion_bytes);
    } catch (const std::exception& e) {
        // Ignore eviction completion errors (same as C#)
        (void)e;
    }
}

std::unique_ptr<WorkflowInstance> WorkflowWorker::create_instance(
    const std::string& workflow_type, const std::string& run_id) {
    // Look up the workflow definition
    std::shared_ptr<workflows::WorkflowDefinition> defn;

    auto it = options_.workflows.find(workflow_type);
    if (it != options_.workflows.end()) {
        defn = it->second;
    } else if (options_.dynamic_workflow) {
        defn = options_.dynamic_workflow;
    } else {
        // Unknown workflow type
        return nullptr;
    }

    WorkflowInstance::Config config;
    config.definition = defn;
    config.data_converter = options_.data_converter;
    config.info.workflow_type = workflow_type;
    config.info.run_id = run_id;
    config.info.task_queue = options_.task_queue;
    config.info.namespace_ = options_.ns;

    return std::make_unique<WorkflowInstance>(std::move(config));
}

std::unique_ptr<WorkflowInstance> WorkflowWorker::create_instance(
    const std::string& workflow_type, const std::string& run_id,
    const coresdk::workflow_activation::InitializeWorkflow& init) {
    // Look up the workflow definition
    std::shared_ptr<workflows::WorkflowDefinition> defn;

    auto it = options_.workflows.find(workflow_type);
    if (it != options_.workflows.end()) {
        defn = it->second;
    } else if (options_.dynamic_workflow) {
        defn = options_.dynamic_workflow;
    } else {
        return nullptr;
    }

    WorkflowInstance::Config config;
    config.definition = defn;
    config.data_converter = options_.data_converter;
    config.randomness_seed = init.randomness_seed();

    // Populate full WorkflowInfo from the InitializeWorkflow protobuf.
    auto& info = config.info;
    info.workflow_type = workflow_type;
    info.run_id = run_id;
    info.workflow_id = init.workflow_id();
    info.task_queue = options_.task_queue;
    info.namespace_ = options_.ns;
    info.attempt = init.attempt();
    info.first_execution_run_id = init.first_execution_run_id();

    if (!init.continued_from_execution_run_id().empty()) {
        info.continued_run_id = init.continued_from_execution_run_id();
    }
    if (!init.cron_schedule().empty()) {
        info.cron_schedule = init.cron_schedule();
    }
    if (init.has_workflow_execution_timeout()) {
        info.execution_timeout =
            to_millis(init.workflow_execution_timeout());
    }
    if (init.has_workflow_run_timeout()) {
        info.run_timeout = to_millis(init.workflow_run_timeout());
    }
    if (init.has_workflow_task_timeout()) {
        info.task_timeout = to_millis(init.workflow_task_timeout());
    }
    if (init.has_start_time()) {
        info.start_time = to_time_point(init.start_time());
    }
    if (init.has_parent_workflow_info()) {
        workflows::ParentWorkflowInfo parent;
        parent.namespace_ = init.parent_workflow_info().namespace_();
        parent.run_id = init.parent_workflow_info().run_id();
        parent.workflow_id = init.parent_workflow_info().workflow_id();
        info.parent = std::move(parent);
    }
    if (init.has_root_workflow()) {
        workflows::RootWorkflowInfo root;
        root.run_id = init.root_workflow().run_id();
        root.workflow_id = init.root_workflow().workflow_id();
        info.root = std::move(root);
    }

    return std::make_unique<WorkflowInstance>(std::move(config));
}

}  // namespace temporalio::worker::internal
