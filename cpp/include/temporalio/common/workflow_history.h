#pragma once

/// @file workflow_history.h
/// @brief WorkflowHistory for replay support.

#include <temporalio/export.h>

#include <string>

namespace temporalio::common {

/// History for a workflow, used for replay and determinism testing.
class TEMPORALIO_EXPORT WorkflowHistory {
public:
    /// Construct with workflow ID and serialized history bytes.
    WorkflowHistory(std::string id, std::string serialized_history)
        : id_(std::move(id)),
          serialized_history_(std::move(serialized_history)) {}

    /// ID of the workflow.
    const std::string& id() const noexcept { return id_; }

    /// Serialized protobuf history bytes.
    const std::string& serialized_history() const noexcept {
        return serialized_history_;
    }

    /// Create a WorkflowHistory from JSON string.
    /// @param workflow_id Workflow ID.
    /// @param json JSON history string.
    /// @return Parsed workflow history.
    static WorkflowHistory from_json(const std::string& workflow_id,
                                     const std::string& json);

    /// Convert this history to JSON string.
    std::string to_json() const;

private:
    std::string id_;
    std::string serialized_history_;
};

} // namespace temporalio::common
