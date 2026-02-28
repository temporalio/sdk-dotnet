#pragma once

/// @file workflow_options.h
/// @brief Options for starting and interacting with workflows.

#include <temporalio/common/enums.h>
#include <temporalio/common/retry_policy.h>
#include <temporalio/common/search_attributes.h>

#include <chrono>
#include <functional>
#include <optional>
#include <string>
#include <unordered_map>
#include <vector>

namespace temporalio::client {

/// Options for starting a workflow execution.
struct WorkflowOptions {
    /// Workflow ID (required).
    std::string id{};

    /// Task queue to run the workflow on (required).
    std::string task_queue{};

    /// Execution timeout for the entire workflow run.
    std::optional<std::chrono::milliseconds> execution_timeout{};

    /// Run timeout for a single workflow run.
    std::optional<std::chrono::milliseconds> run_timeout{};

    /// Task timeout for a single workflow task.
    std::optional<std::chrono::milliseconds> task_timeout{};

    /// Workflow ID reuse policy.
    common::WorkflowIdReusePolicy id_reuse_policy{
        common::WorkflowIdReusePolicy::kUnspecified};

    /// Workflow ID conflict policy.
    common::WorkflowIdConflictPolicy id_conflict_policy{
        common::WorkflowIdConflictPolicy::kUnspecified};

    /// Retry policy for the workflow.
    std::optional<common::RetryPolicy> retry_policy{};

    /// Cron schedule string.
    std::optional<std::string> cron_schedule{};

    /// Memo key-value pairs.
    std::unordered_map<std::string, std::string> memo{};

    /// Search attributes.
    std::optional<common::SearchAttributeCollection> search_attributes{};

    /// Start delay before the workflow runs.
    std::optional<std::chrono::milliseconds> start_delay{};

    /// Request ID for idempotent start.
    std::optional<std::string> request_id{};

    /// Priority for task scheduling.
    std::optional<common::Priority> priority{};
};

/// Options for signaling a workflow.
struct WorkflowSignalOptions {
    /// Request ID for idempotency.
    std::optional<std::string> request_id{};
};

/// Options for querying a workflow.
struct WorkflowQueryOptions {
    /// Query reject condition.
    std::optional<int> reject_condition{};
};

/// Options for canceling a workflow.
struct WorkflowCancelOptions {};

/// Options for terminating a workflow.
struct WorkflowTerminateOptions {
    /// Reason for termination.
    std::optional<std::string> reason{};
};

/// Options for updating a workflow.
struct WorkflowUpdateOptions {
    /// Update ID. If not set, server will generate one.
    std::optional<std::string> update_id{};

    /// Wait stage. Default is kCompleted (wait for update to finish).
    /// Use kAccepted to return as soon as the update is accepted.
    enum class WaitStage {
        /// Wait for update to complete (default).
        kCompleted = 3,
        /// Wait only for update to be accepted.
        kAccepted = 2,
        /// Wait only for update to be admitted.
        kAdmitted = 1,
    };

    /// How far to wait for the update lifecycle. Default: kCompleted.
    WaitStage wait_stage{WaitStage::kCompleted};
};

/// Options for describing a workflow.
struct WorkflowDescribeOptions {};

/// Options for getting workflow history.
struct WorkflowHistoryFetchOptions {};

/// Options for counting workflows.
struct WorkflowCountOptions {
    /// Visibility query string.
    std::optional<std::string> query{};
};

/// Options for listing workflows.
struct WorkflowListOptions {
    /// Visibility query string.
    std::optional<std::string> query{};

    /// Page size for listing.
    std::optional<int> page_size{};
};

} // namespace temporalio::client
