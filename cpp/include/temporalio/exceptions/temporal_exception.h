#pragma once

/// @file Temporal SDK exception hierarchy for workflow and activity failures.

#include <temporalio/export.h>

#include <chrono>
#include <cstdint>
#include <exception>
#include <memory>
#include <optional>
#include <stdexcept>
#include <string>
#include <vector>

namespace temporalio::exceptions {

/// Base exception for all custom exceptions thrown by the Temporal library.
class TEMPORALIO_EXPORT TemporalException : public std::runtime_error {
public:
    /// Check whether the given exception is a cancellation (Temporal or
    /// standard).
    static bool is_canceled_exception(const std::exception_ptr& e);

    /// Returns the inner (cause) exception, if any.
    std::exception_ptr inner() const noexcept { return inner_; }

protected:
    explicit TemporalException(const std::string& message);
    TemporalException(const std::string& message, std::exception_ptr inner);
    ~TemporalException() override = default;

private:
    std::exception_ptr inner_;
};

/// Exception representing a gRPC failure.
class TEMPORALIO_EXPORT RpcException : public TemporalException {
public:
    /// gRPC status codes.
    enum class StatusCode : int {
        kOk = 0,
        kCancelled = 1,
        kUnknown = 2,
        kInvalidArgument = 3,
        kDeadlineExceeded = 4,
        kNotFound = 5,
        kAlreadyExists = 6,
        kPermissionDenied = 7,
        kResourceExhausted = 8,
        kFailedPrecondition = 9,
        kAborted = 10,
        kOutOfRange = 11,
        kUnimplemented = 12,
        kInternal = 13,
        kUnavailable = 14,
        kDataLoss = 15,
        kUnauthenticated = 16,
    };

    RpcException(StatusCode code, const std::string& message,
                 std::vector<uint8_t> raw_status = {});

    /// The gRPC status code.
    StatusCode code() const noexcept { return code_; }

    /// The raw gRPC status bytes (may be empty).
    const std::vector<uint8_t>& raw_status() const noexcept {
        return raw_status_;
    }

private:
    StatusCode code_;
    std::vector<uint8_t> raw_status_;
};

/// Exception representing timeout or cancellation on some client calls.
class TEMPORALIO_EXPORT RpcTimeoutOrCanceledException : public TemporalException {
public:
    RpcTimeoutOrCanceledException(const std::string& message,
                                  std::exception_ptr inner = nullptr);
};

/// Specialization for workflow update RPC timeout/cancellation.
class TEMPORALIO_EXPORT WorkflowUpdateRpcTimeoutOrCanceledException
    : public RpcTimeoutOrCanceledException {
public:
    explicit WorkflowUpdateRpcTimeoutOrCanceledException(
        std::exception_ptr inner = nullptr);
};

/// Base exception for all Temporal-failure-based exceptions.
/// All exceptions of this type fail a workflow.
class TEMPORALIO_EXPORT FailureException : public TemporalException {
public:
    /// The stack trace from the failure, if any.
    const std::string& failure_stack_trace() const noexcept {
        return failure_stack_trace_;
    }

protected:
    explicit FailureException(const std::string& message,
                              std::exception_ptr inner = nullptr);
    FailureException(const std::string& message,
                     const std::string& failure_stack_trace,
                     std::exception_ptr inner = nullptr);
    ~FailureException() override = default;

private:
    std::string failure_stack_trace_;
};

/// Exception representing an error in user code.
/// In workflows, users should throw this to signal a workflow failure.
/// In activities, non-Temporal exceptions are translated to this.
class TEMPORALIO_EXPORT ApplicationFailureException : public FailureException {
public:
    ApplicationFailureException(
        const std::string& message,
        std::optional<std::string> error_type = std::nullopt,
        bool non_retryable = false,
        std::optional<std::chrono::milliseconds> next_retry_delay =
            std::nullopt,
        std::exception_ptr inner = nullptr);

    /// The string "type" of the exception, if any.
    const std::optional<std::string>& error_type() const noexcept {
        return error_type_;
    }

    /// Whether this exception is non-retryable.
    bool non_retryable() const noexcept { return non_retryable_; }

    /// Override for the next retry delay, if set.
    std::optional<std::chrono::milliseconds> next_retry_delay() const noexcept {
        return next_retry_delay_;
    }

private:
    std::optional<std::string> error_type_;
    bool non_retryable_;
    std::optional<std::chrono::milliseconds> next_retry_delay_;
};

/// Exception representing a cancellation.
class TEMPORALIO_EXPORT CanceledFailureException : public FailureException {
public:
    explicit CanceledFailureException(const std::string& message,
                                      std::exception_ptr inner = nullptr);
};

/// Exception representing a terminated workflow.
class TEMPORALIO_EXPORT TerminatedFailureException : public FailureException {
public:
    explicit TerminatedFailureException(const std::string& message,
                                        std::exception_ptr inner = nullptr);
};

/// Exception representing a timeout.
class TEMPORALIO_EXPORT TimeoutFailureException : public FailureException {
public:
    /// Timeout types matching the protobuf TimeoutType enum.
    enum class TimeoutType : int {
        kUnspecified = 0,
        kStartToClose = 1,
        kScheduleToStart = 2,
        kScheduleToClose = 3,
        kHeartbeat = 4,
    };

    TimeoutFailureException(const std::string& message,
                            TimeoutType timeout_type,
                            std::exception_ptr inner = nullptr);

    /// The type of timeout that occurred.
    TimeoutType timeout_type() const noexcept { return timeout_type_; }

private:
    TimeoutType timeout_type_;
};

/// Exception representing a server failure.
class TEMPORALIO_EXPORT ServerFailureException : public FailureException {
public:
    ServerFailureException(const std::string& message, bool non_retryable,
                           std::exception_ptr inner = nullptr);

    /// Whether this failure is non-retryable.
    bool non_retryable() const noexcept { return non_retryable_; }

private:
    bool non_retryable_;
};

/// Exception thrown to workflows when an activity fails.
class TEMPORALIO_EXPORT ActivityFailureException : public FailureException {
public:
    ActivityFailureException(const std::string& message,
                             std::string activity_type,
                             std::string activity_id,
                             std::optional<std::string> identity = std::nullopt,
                             int retry_state = 0,
                             std::exception_ptr inner = nullptr);

    /// The activity name/type that failed.
    const std::string& activity_type() const noexcept {
        return activity_type_;
    }

    /// The identifier of the activity that failed.
    const std::string& activity_id() const noexcept { return activity_id_; }

    /// The identity of the worker where this failed.
    const std::optional<std::string>& identity() const noexcept {
        return identity_;
    }

    /// The retry state for the failure (maps to RetryState enum).
    int retry_state() const noexcept { return retry_state_; }

private:
    std::string activity_type_;
    std::string activity_id_;
    std::optional<std::string> identity_;
    int retry_state_;
};

/// Exception representing a child workflow failure.
class TEMPORALIO_EXPORT ChildWorkflowFailureException : public FailureException {
public:
    ChildWorkflowFailureException(const std::string& message,
                                  std::string ns,
                                  std::string workflow_id,
                                  std::string run_id,
                                  std::string workflow_type,
                                  int retry_state = 0,
                                  std::exception_ptr inner = nullptr);

    /// The namespace of the failed child workflow.
    const std::string& ns() const noexcept { return namespace_; }

    /// The ID of the failed child workflow.
    const std::string& workflow_id() const noexcept { return workflow_id_; }

    /// The run ID of the failed child workflow.
    const std::string& run_id() const noexcept { return run_id_; }

    /// The child workflow type that failed.
    const std::string& workflow_type() const noexcept {
        return workflow_type_;
    }

    /// The retry state of the failure.
    int retry_state() const noexcept { return retry_state_; }

private:
    std::string namespace_;
    std::string workflow_id_;
    std::string run_id_;
    std::string workflow_type_;
    int retry_state_;
};

/// Exception thrown to workflows when a Nexus operation fails.
class TEMPORALIO_EXPORT NexusOperationFailureException : public FailureException {
public:
    NexusOperationFailureException(const std::string& message,
                                   std::string endpoint,
                                   std::string service,
                                   std::string operation,
                                   std::string operation_token = {},
                                   std::exception_ptr inner = nullptr);

    const std::string& endpoint() const noexcept { return endpoint_; }
    const std::string& service() const noexcept { return service_; }
    const std::string& operation() const noexcept { return operation_; }
    const std::string& operation_token() const noexcept {
        return operation_token_;
    }

private:
    std::string endpoint_;
    std::string service_;
    std::string operation_;
    std::string operation_token_;
};

/// Failure from a Nexus handler.
class TEMPORALIO_EXPORT NexusHandlerFailureException : public FailureException {
public:
    NexusHandlerFailureException(const std::string& message,
                                 std::string raw_error_type,
                                 int retry_behavior = 0,
                                 std::exception_ptr inner = nullptr);

    const std::string& raw_error_type() const noexcept {
        return raw_error_type_;
    }

    /// Error retry behavior (maps to NexusHandlerErrorRetryBehavior enum).
    int retry_behavior() const noexcept { return retry_behavior_; }

private:
    std::string raw_error_type_;
    int retry_behavior_;
};

/// Exception thrown when attempting to start a workflow that was already
/// started.
class TEMPORALIO_EXPORT WorkflowAlreadyStartedException : public FailureException {
public:
    WorkflowAlreadyStartedException(const std::string& message,
                                    std::string workflow_id,
                                    std::string workflow_type,
                                    std::string run_id);

    const std::string& workflow_id() const noexcept { return workflow_id_; }
    const std::string& workflow_type() const noexcept {
        return workflow_type_;
    }
    const std::string& run_id() const noexcept { return run_id_; }

private:
    std::string workflow_id_;
    std::string workflow_type_;
    std::string run_id_;
};

/// Exception thrown when attempting to start a standalone activity that was
/// already started.
class TEMPORALIO_EXPORT ActivityAlreadyStartedException : public FailureException {
public:
    ActivityAlreadyStartedException(const std::string& message,
                                    std::string activity_id,
                                    std::string activity_type,
                                    std::optional<std::string> run_id = {});

    const std::string& activity_id() const noexcept { return activity_id_; }
    const std::string& activity_type() const noexcept {
        return activity_type_;
    }
    const std::optional<std::string>& run_id() const noexcept {
        return run_id_;
    }

private:
    std::string activity_id_;
    std::string activity_type_;
    std::optional<std::string> run_id_;
};

/// Exception thrown when a schedule is already running.
class TEMPORALIO_EXPORT ScheduleAlreadyRunningException : public FailureException {
public:
    ScheduleAlreadyRunningException();
};

/// Exception thrown when a workflow has failed while waiting for the result.
class TEMPORALIO_EXPORT WorkflowFailedException : public TemporalException {
public:
    explicit WorkflowFailedException(std::exception_ptr inner = nullptr);
};

/// Exception thrown when a standalone activity has failed while waiting for
/// the result.
class TEMPORALIO_EXPORT ActivityFailedException : public TemporalException {
public:
    explicit ActivityFailedException(std::exception_ptr inner = nullptr);
};

/// Exception thrown from within a workflow to trigger continue-as-new.
/// This is caught by the WorkflowInstance's RunTopLevel wrapper and
/// converted into a kContinueAsNew command.
class TEMPORALIO_EXPORT ContinueAsNewException : public TemporalException {
public:
    explicit ContinueAsNewException(
        std::string workflow_type = {},
        std::string task_queue = {});

    const std::string& workflow_type() const noexcept {
        return workflow_type_;
    }
    const std::string& task_queue() const noexcept { return task_queue_; }

private:
    std::string workflow_type_;
    std::string task_queue_;
};

/// Thrown when a workflow continues as new and the caller is not following
/// runs.
class TEMPORALIO_EXPORT WorkflowContinuedAsNewException : public TemporalException {
public:
    explicit WorkflowContinuedAsNewException(std::string new_run_id);

    const std::string& new_run_id() const noexcept { return new_run_id_; }

private:
    std::string new_run_id_;
};

/// Thrown when a query fails on the worker.
class TEMPORALIO_EXPORT WorkflowQueryFailedException : public TemporalException {
public:
    explicit WorkflowQueryFailedException(const std::string& message);
};

/// Thrown when a query is rejected by the server due to bad workflow status.
class TEMPORALIO_EXPORT WorkflowQueryRejectedException : public TemporalException {
public:
    explicit WorkflowQueryRejectedException(int workflow_status);

    /// The workflow execution status causing rejection (maps to
    /// WorkflowExecutionStatus enum).
    int workflow_status() const noexcept { return workflow_status_; }

private:
    int workflow_status_;
};

/// Exception thrown when a workflow update has failed.
class TEMPORALIO_EXPORT WorkflowUpdateFailedException : public TemporalException {
public:
    explicit WorkflowUpdateFailedException(std::exception_ptr inner = nullptr);
};

/// Thrown from async activity heartbeat when cancellation is requested.
class TEMPORALIO_EXPORT AsyncActivityCanceledException : public TemporalException {
public:
    AsyncActivityCanceledException();
};

/// Occurs when the workflow has done something invalid.
class TEMPORALIO_EXPORT InvalidWorkflowOperationException : public TemporalException {
public:
    explicit InvalidWorkflowOperationException(const std::string& message);
};

/// Occurs when the workflow does something non-deterministic.
class TEMPORALIO_EXPORT WorkflowNondeterminismException
    : public InvalidWorkflowOperationException {
public:
    explicit WorkflowNondeterminismException(const std::string& message);
};

/// Occurs when the workflow does something outside of the workflow scheduler.
class TEMPORALIO_EXPORT InvalidWorkflowSchedulerException
    : public InvalidWorkflowOperationException {
public:
    InvalidWorkflowSchedulerException(
        const std::string& message,
        std::optional<std::string> stack_trace_override = std::nullopt);

    const char* what() const noexcept override;

private:
    std::optional<std::string> stack_trace_override_;
    mutable std::string what_cache_;
};

}  // namespace temporalio::exceptions

