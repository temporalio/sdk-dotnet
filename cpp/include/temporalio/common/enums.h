#pragma once

/// @file enums.h
/// @brief Common enumerations used across the Temporal SDK.

#include <cstdint>
#include <optional>
#include <string>

namespace temporalio::common {

/// Specifies when a workflow might move from one Build ID to another.
enum class VersioningBehavior : int {
    kUnspecified = 0,
    kPinned = 1,
    kAutoUpgrade = 2,
};

/// Parent close policy for child workflows.
enum class ParentClosePolicy : int {
    kUnspecified = 0,
    kTerminate = 1,
    kAbandon = 2,
    kRequestCancel = 3,
};

/// Policy for handling workflow ID conflicts.
enum class WorkflowIdConflictPolicy : int {
    kUnspecified = 0,
    kFail = 1,
    kUseExisting = 2,
    kTerminateExisting = 3,
};

/// Policy for reusing workflow IDs.
enum class WorkflowIdReusePolicy : int {
    kUnspecified = 0,
    kAllowDuplicate = 1,
    kAllowDuplicateFailedOnly = 2,
    kRejectDuplicate = 3,
    kTerminateIfRunning = 4,
};

/// Status of a workflow execution.
enum class WorkflowExecutionStatus : int {
    kUnspecified = 0,
    kRunning = 1,
    kCompleted = 2,
    kFailed = 3,
    kCanceled = 4,
    kTerminated = 5,
    kContinuedAsNew = 6,
    kTimedOut = 7,
    kPaused = 8,
};

/// Task reachability for a worker build ID.
enum class TaskReachability : int {
    kUnspecified = 0,
    kNewWorkflows = 1,
    kExistingWorkflows = 2,
    kOpenWorkflows = 3,
    kClosedWorkflows = 4,
};

/// Priority metadata for controlling task processing order.
struct Priority {
    /// Priority key (1-n, lower = higher priority). Nullopt means use default.
    std::optional<int> priority_key{};

    /// Fairness key for proportional dispatch. Nullopt means use default.
    std::optional<std::string> fairness_key{};

    /// Fairness weight (clamped to [0.001, 1000]). Nullopt means use default.
    std::optional<float> fairness_weight{};

    bool operator==(const Priority&) const = default;
};

} // namespace temporalio::common
