#pragma once

/// @file worker_interceptor.h
/// @brief Worker-side interceptor chain for workflows, activities, and Nexus.
///
/// Mirrors the C# Temporalio.Worker.Interceptors namespace. Two independent
/// chains: workflow (inbound + outbound) and activity (inbound + outbound).
/// Each interceptor wraps the next in the chain (decorator pattern).

#include <any>
#include <chrono>
#include <memory>
#include <stdexcept>
#include <string>
#include <unordered_map>
#include <vector>

#include <temporalio/coro/task.h>

namespace temporalio::activities {
class ActivityDefinition;
}

namespace temporalio::workflows {
struct WorkflowSignalDefinition;
struct WorkflowQueryDefinition;
struct WorkflowUpdateDefinition;
}  // namespace temporalio::workflows

namespace temporalio::worker::interceptors {

// ============================================================================
// Input types (mirror C# record types in Interceptors/)
// ============================================================================

/// Input for WorkflowInboundInterceptor::execute_workflow_async().
struct ExecuteWorkflowInput {
    /// Workflow instance (type-erased, shared ownership).
    std::shared_ptr<void> instance;
    /// Arguments for the run method.
    std::vector<std::any> args;
};

/// Input for WorkflowInboundInterceptor::handle_signal_async().
struct HandleSignalInput {
    /// Signal name.
    std::string signal;
    /// Signal definition (may be null for dynamic signals).
    const workflows::WorkflowSignalDefinition* definition{nullptr};
    /// Signal arguments.
    std::vector<std::any> args;
    /// Optional headers.
    std::unordered_map<std::string, std::string> headers;
};

/// Input for WorkflowInboundInterceptor::handle_query().
struct HandleQueryInput {
    /// Query ID.
    std::string id;
    /// Query name.
    std::string query;
    /// Query definition (may be null for dynamic queries).
    const workflows::WorkflowQueryDefinition* definition{nullptr};
    /// Query arguments.
    std::vector<std::any> args;
    /// Optional headers.
    std::unordered_map<std::string, std::string> headers;
};

/// Input for WorkflowInboundInterceptor::validate_update() and
/// handle_update_async().
struct HandleUpdateInput {
    /// Update ID.
    std::string id;
    /// Update name.
    std::string update;
    /// Update definition (may be null for dynamic updates).
    const workflows::WorkflowUpdateDefinition* definition{nullptr};
    /// Update arguments.
    std::vector<std::any> args;
    /// Optional headers.
    std::unordered_map<std::string, std::string> headers;
};

/// Input for WorkflowOutboundInterceptor::delay_async().
struct DelayAsyncInput {
    /// Delay duration.
    std::chrono::milliseconds delay{0};
    /// Optional summary.
    std::string summary;
};

/// Input for WorkflowOutboundInterceptor::schedule_activity_async().
struct ScheduleActivityInput {
    /// Activity type name.
    std::string activity;
    /// Activity args.
    std::vector<std::any> args;
    /// Optional headers.
    std::unordered_map<std::string, std::string> headers;
};

/// Input for WorkflowOutboundInterceptor::schedule_local_activity_async().
struct ScheduleLocalActivityInput {
    /// Activity type name.
    std::string activity;
    /// Activity args.
    std::vector<std::any> args;
    /// Optional headers.
    std::unordered_map<std::string, std::string> headers;
};

/// Input for WorkflowOutboundInterceptor::start_child_workflow_async().
struct StartChildWorkflowInput {
    /// Workflow type name.
    std::string workflow;
    /// Workflow args.
    std::vector<std::any> args;
    /// Optional headers.
    std::unordered_map<std::string, std::string> headers;
};

/// Input for WorkflowOutboundInterceptor::signal_child_workflow_async().
struct SignalChildWorkflowInput {
    /// Signal name.
    std::string signal;
    /// Signal args.
    std::vector<std::any> args;
};

/// Input for WorkflowOutboundInterceptor::signal_external_workflow_async().
struct SignalExternalWorkflowInput {
    /// Workflow ID.
    std::string workflow_id;
    /// Run ID (optional).
    std::string run_id;
    /// Signal name.
    std::string signal;
    /// Signal args.
    std::vector<std::any> args;
};

/// Input for WorkflowOutboundInterceptor::cancel_external_workflow_async().
struct CancelExternalWorkflowInput {
    /// Workflow ID.
    std::string workflow_id;
    /// Run ID (optional).
    std::string run_id;
};

/// Input for ActivityInboundInterceptor::execute_activity_async().
struct ExecuteActivityInput {
    /// Activity definition.
    const activities::ActivityDefinition* activity{nullptr};
    /// Activity arguments.
    std::vector<std::any> args;
    /// Optional headers.
    std::unordered_map<std::string, std::string> headers;
};

/// Input for ActivityOutboundInterceptor::heartbeat().
struct HeartbeatInput {
    /// Heartbeat details.
    std::vector<std::any> details;
};

// ============================================================================
// Outbound interceptors
// ============================================================================

/// Outbound interceptor for workflow calls originating from workflow code.
/// Each method delegates to the next interceptor in the chain.
class WorkflowOutboundInterceptor {
public:
    /// Construct with the next interceptor in the chain.
    explicit WorkflowOutboundInterceptor(WorkflowOutboundInterceptor* next)
        : next_(next) {}

    virtual ~WorkflowOutboundInterceptor() = default;

    /// Intercept timer creation (Workflow::delay).
    virtual coro::Task<void> delay_async(DelayAsyncInput input) {
        return next().delay_async(std::move(input));
    }

    /// Intercept scheduling a remote activity.
    virtual coro::Task<std::any> schedule_activity_async(
        ScheduleActivityInput input) {
        return next().schedule_activity_async(std::move(input));
    }

    /// Intercept scheduling a local activity.
    virtual coro::Task<std::any> schedule_local_activity_async(
        ScheduleLocalActivityInput input) {
        return next().schedule_local_activity_async(std::move(input));
    }

    /// Intercept starting a child workflow.
    virtual coro::Task<std::any> start_child_workflow_async(
        StartChildWorkflowInput input) {
        return next().start_child_workflow_async(std::move(input));
    }

    /// Intercept signalling a child workflow.
    virtual coro::Task<void> signal_child_workflow_async(
        SignalChildWorkflowInput input) {
        return next().signal_child_workflow_async(std::move(input));
    }

    /// Intercept signalling an external workflow.
    virtual coro::Task<void> signal_external_workflow_async(
        SignalExternalWorkflowInput input) {
        return next().signal_external_workflow_async(std::move(input));
    }

    /// Intercept cancelling an external workflow.
    virtual coro::Task<void> cancel_external_workflow_async(
        CancelExternalWorkflowInput input) {
        return next().cancel_external_workflow_async(std::move(input));
    }

protected:
    /// Get the next interceptor in the chain.
    WorkflowOutboundInterceptor& next() {
        if (!next_) {
            throw std::runtime_error("No next interceptor in chain");
        }
        return *next_;
    }

    /// Direct constructor for the root interceptor (no next).
    WorkflowOutboundInterceptor() : next_(nullptr) {}

private:
    WorkflowOutboundInterceptor* next_;
};

/// Outbound interceptor for activity calls originating from activity code.
class ActivityOutboundInterceptor {
public:
    /// Construct with the next interceptor in the chain.
    explicit ActivityOutboundInterceptor(ActivityOutboundInterceptor* next)
        : next_(next) {}

    virtual ~ActivityOutboundInterceptor() = default;

    /// Intercept heartbeat.
    virtual void heartbeat(HeartbeatInput input) {
        next().heartbeat(std::move(input));
    }

protected:
    /// Get the next interceptor in the chain.
    ActivityOutboundInterceptor& next() {
        if (!next_) {
            throw std::runtime_error("No next interceptor in chain");
        }
        return *next_;
    }

    /// Direct constructor for the root interceptor (no next).
    ActivityOutboundInterceptor() : next_(nullptr) {}

private:
    ActivityOutboundInterceptor* next_;
};

// ============================================================================
// Inbound interceptors
// ============================================================================

/// Inbound interceptor for workflow calls from the server.
/// Each method delegates to the next interceptor in the chain.
class WorkflowInboundInterceptor {
public:
    /// Construct with the next interceptor in the chain.
    explicit WorkflowInboundInterceptor(WorkflowInboundInterceptor* next)
        : next_(next) {}

    virtual ~WorkflowInboundInterceptor() = default;

    /// Initialize with an outbound interceptor. Override to wrap/replace
    /// the outbound before forwarding.
    virtual void init(WorkflowOutboundInterceptor* outbound) {
        next().init(outbound);
    }

    /// Intercept workflow execution (the run function).
    virtual coro::Task<std::any> execute_workflow_async(
        ExecuteWorkflowInput input) {
        return next().execute_workflow_async(std::move(input));
    }

    /// Intercept signal handling.
    virtual coro::Task<void> handle_signal_async(HandleSignalInput input) {
        return next().handle_signal_async(std::move(input));
    }

    /// Intercept query handling (synchronous).
    virtual std::any handle_query(HandleQueryInput input) {
        return next().handle_query(std::move(input));
    }

    /// Intercept update validation (synchronous).
    virtual void validate_update(HandleUpdateInput input) {
        next().validate_update(std::move(input));
    }

    /// Intercept update handling.
    virtual coro::Task<std::any> handle_update_async(
        HandleUpdateInput input) {
        return next().handle_update_async(std::move(input));
    }

protected:
    /// Get the next interceptor in the chain.
    WorkflowInboundInterceptor& next() {
        if (!next_) {
            throw std::runtime_error("No next interceptor in chain");
        }
        return *next_;
    }

    /// Direct constructor for the root interceptor (no next).
    WorkflowInboundInterceptor() : next_(nullptr) {}

private:
    WorkflowInboundInterceptor* next_;
};

/// Inbound interceptor for activity calls from the server.
class ActivityInboundInterceptor {
public:
    /// Construct with the next interceptor in the chain.
    explicit ActivityInboundInterceptor(ActivityInboundInterceptor* next)
        : next_(next) {}

    virtual ~ActivityInboundInterceptor() = default;

    /// Initialize with an outbound interceptor. Override to wrap/replace
    /// the outbound before forwarding.
    virtual void init(ActivityOutboundInterceptor* outbound) {
        next().init(outbound);
    }

    /// Intercept activity execution.
    virtual coro::Task<std::any> execute_activity_async(
        ExecuteActivityInput input) {
        return next().execute_activity_async(std::move(input));
    }

protected:
    /// Get the next interceptor in the chain.
    ActivityInboundInterceptor& next() {
        if (!next_) {
            throw std::runtime_error("No next interceptor in chain");
        }
        return *next_;
    }

    /// Direct constructor for the root interceptor (no next).
    ActivityInboundInterceptor() : next_(nullptr) {}

private:
    ActivityInboundInterceptor* next_;
};

// ============================================================================
// Nexus operation interceptors (experimental)
// ============================================================================

/// Input for NexusOperationInboundInterceptor::execute_start_async().
/// WARNING: Nexus support is experimental.
struct ExecuteNexusOperationStartInput {
    /// Operation name.
    std::string operation;
    /// Operation input (type-erased).
    std::any input;
    /// Optional headers.
    std::unordered_map<std::string, std::string> headers;
};

/// Input for NexusOperationInboundInterceptor::execute_cancel_async().
/// WARNING: Nexus support is experimental.
struct ExecuteNexusOperationCancelInput {
    /// Operation name.
    std::string operation;
    /// Operation token.
    std::string token;
};

/// Inbound interceptor for Nexus operation calls from the server.
/// WARNING: Nexus support is experimental.
class NexusOperationInboundInterceptor {
public:
    /// Construct with the next interceptor in the chain.
    explicit NexusOperationInboundInterceptor(
        NexusOperationInboundInterceptor* next)
        : next_(next) {}

    virtual ~NexusOperationInboundInterceptor() = default;

    /// Intercept Nexus operation start.
    virtual coro::Task<std::any> execute_start_async(
        ExecuteNexusOperationStartInput input) {
        return next().execute_start_async(std::move(input));
    }

    /// Intercept Nexus operation cancel.
    virtual coro::Task<void> execute_cancel_async(
        ExecuteNexusOperationCancelInput input) {
        return next().execute_cancel_async(std::move(input));
    }

protected:
    /// Get the next interceptor in the chain.
    NexusOperationInboundInterceptor& next() {
        if (!next_) {
            throw std::runtime_error("No next interceptor in chain");
        }
        return *next_;
    }

    /// Direct constructor for the root interceptor (no next).
    NexusOperationInboundInterceptor() : next_(nullptr) {}

private:
    NexusOperationInboundInterceptor* next_;
};

// ============================================================================
// Worker interceptor factory interface
// ============================================================================

/// Interface for creating worker-side interceptors.
/// Implementations can return the next interceptor unchanged (pass-through)
/// or wrap it with custom logic.
class IWorkerInterceptor {
public:
    virtual ~IWorkerInterceptor() = default;

    /// Create a workflow inbound interceptor that wraps the given next
    /// interceptor.
    /// @param next The next interceptor in the chain. Lifetime: owned by the
    ///   IWorkerInterceptor factory -- the raw pointer is by design and the
    ///   caller retains ownership.
    /// @return A pointer to the interceptor to use. Return next for
    /// pass-through.
    virtual WorkflowInboundInterceptor* intercept_workflow(
        WorkflowInboundInterceptor* next) {
        return next;
    }

    /// Create an activity inbound interceptor that wraps the given next
    /// interceptor.
    /// @param next The next interceptor in the chain.
    /// @return A pointer to the interceptor to use. Return next for
    /// pass-through.
    virtual ActivityInboundInterceptor* intercept_activity(
        ActivityInboundInterceptor* next) {
        return next;
    }

    /// Create a Nexus operation inbound interceptor that wraps the given
    /// next interceptor.
    /// @param next The next interceptor in the chain.
    /// @return A pointer to the interceptor to use. Return next for
    /// pass-through.
    /// WARNING: Nexus support is experimental.
    virtual NexusOperationInboundInterceptor* intercept_nexus_operation(
        NexusOperationInboundInterceptor* next) {
        return next;
    }
};

}  // namespace temporalio::worker::interceptors

