#pragma once

/// @file operation_handler.h
/// @brief Nexus RPC operation handlers and context types.
/// WARNING: Nexus support is experimental.

#include <temporalio/export.h>
#include <temporalio/coro/task.h>

#include <any>
#include <functional>
#include <memory>
#include <optional>
#include <string>
#include <unordered_map>
#include <vector>

namespace temporalio::client {
class TemporalClient;
} // namespace temporalio::client

namespace temporalio::nexus {

// ── NexusOperationInfo ──────────────────────────────────────────────────────

/// Temporal-specific Nexus operation information.
struct NexusOperationInfo {
    /// Current namespace.
    std::string ns;

    /// Current task queue.
    std::string task_queue;
};

// ── NexusLink ───────────────────────────────────────────────────────────────

/// A Nexus link (URI + type descriptor name).
struct NexusLink {
    /// Link URI (e.g., temporal:///namespaces/ns/workflows/wid/rid/history).
    std::string uri;

    /// Link type descriptor name.
    std::string type;
};

// ── Context types ───────────────────────────────────────────────────────────

/// Base context for a Nexus operation handler invocation.
struct OperationContext {
    /// Service name.
    std::string service;

    /// Operation name.
    std::string operation;

    /// Headers from the caller.
    std::unordered_map<std::string, std::string> headers;
};

/// Context for a Nexus start operation.
struct OperationStartContext : OperationContext {
    /// Request ID for idempotency.
    std::optional<std::string> request_id;

    /// Callback URL for async completion notification.
    std::optional<std::string> callback_url;

    /// Callback headers.
    std::unordered_map<std::string, std::string> callback_headers;

    /// Inbound links from the caller.
    std::vector<NexusLink> inbound_links;

    /// Outbound links to attach to the response (populated by handler).
    std::vector<NexusLink> outbound_links;
};

/// Context for a Nexus cancel operation.
struct OperationCancelContext : OperationContext {
    /// Operation token identifying the operation to cancel.
    std::string operation_token;
};

/// Context for a Nexus fetch-result operation.
struct OperationFetchResultContext : OperationContext {
    /// Operation token identifying the operation.
    std::string operation_token;
};

/// Context for a Nexus fetch-info operation.
struct OperationFetchInfoContext : OperationContext {
    /// Operation token identifying the operation.
    std::string operation_token;
};

// ── OperationStartResult ────────────────────────────────────────────────────

/// Result of a Nexus operation start call.
struct OperationStartResult {
    /// Whether the operation completed synchronously.
    bool is_sync{false};

    /// Synchronous result value (only valid if is_sync is true).
    std::any sync_result;

    /// Async operation token (only valid if is_sync is false).
    std::string async_operation_token;

    /// Create a synchronous result.
    static OperationStartResult sync(std::any result) {
        return {true, std::move(result), {}};
    }

    /// Create an async result with the given operation token.
    static OperationStartResult async_result(std::string token) {
        return {false, {}, std::move(token)};
    }
};

/// Info about a running Nexus operation.
struct NexusOperationState {
    /// Current state description.
    std::string state;
};

// ── Handler error ───────────────────────────────────────────────────────────

/// Error types for Nexus handler errors.
enum class HandlerErrorType {
    kBadRequest,
    kUnauthenticated,
    kUnauthorized,
    kNotFound,
    kResourceExhausted,
    kInternal,
    kNotImplemented,
    kUnavailable,
    kUpstreamTimeout,
};

/// Exception type for Nexus handler errors with a specific error type.
class OperationException : public std::runtime_error {
public:
    OperationException(HandlerErrorType type, const std::string& msg)
        : std::runtime_error(msg), type_(type) {}

    /// Get the handler error type.
    HandlerErrorType error_type() const noexcept { return type_; }

    /// Get the error type as a string for the Nexus protocol.
    std::string error_type_string() const {
        switch (type_) {
            case HandlerErrorType::kBadRequest: return "BAD_REQUEST";
            case HandlerErrorType::kUnauthenticated: return "UNAUTHENTICATED";
            case HandlerErrorType::kUnauthorized: return "UNAUTHORIZED";
            case HandlerErrorType::kNotFound: return "NOT_FOUND";
            case HandlerErrorType::kResourceExhausted: return "RESOURCE_EXHAUSTED";
            case HandlerErrorType::kInternal: return "INTERNAL";
            case HandlerErrorType::kNotImplemented: return "NOT_IMPLEMENTED";
            case HandlerErrorType::kUnavailable: return "UNAVAILABLE";
            case HandlerErrorType::kUpstreamTimeout: return "UPSTREAM_TIMEOUT";
            default: return "INTERNAL";
        }
    }

private:
    HandlerErrorType type_;
};

// ── NexusOperationExecutionContext ──────────────────────────────────────────

/// Nexus operation context available in Temporal-powered Nexus operations.
/// Provides access to operation info, the Temporal client, and metrics.
///
/// Use current() to access the context during operation execution.
class TEMPORALIO_EXPORT NexusOperationExecutionContext {
public:
    NexusOperationExecutionContext(OperationContext handler_context,
                                  NexusOperationInfo info,
                                  std::shared_ptr<client::TemporalClient> client);

    /// Get the Temporal-specific info for this operation.
    const NexusOperationInfo& info() const noexcept { return info_; }

    /// Get the Nexus handler context.
    const OperationContext& handler_context() const noexcept {
        return handler_context_;
    }

    /// Get the Temporal client for use within the operation.
    /// @throws std::logic_error if no client is available.
    client::TemporalClient& temporal_client() const;

    /// Get the Temporal client shared_ptr.
    std::shared_ptr<client::TemporalClient> temporal_client_ptr() const noexcept {
        return client_;
    }

    /// Get the current thread-local context (set during operation execution).
    /// @throws std::logic_error if no context is active.
    static NexusOperationExecutionContext& current();

    /// Whether there is a current context.
    static bool has_current() noexcept;

    /// Set the current thread-local context (called by ContextScope / NexusWorker).
    static void set_current(NexusOperationExecutionContext* ctx) noexcept;

private:
    friend class ContextScope;

    OperationContext handler_context_;
    NexusOperationInfo info_;
    std::shared_ptr<client::TemporalClient> client_;
};

/// RAII scope for setting and restoring the Nexus operation context.
/// Sets the thread-local NexusOperationExecutionContext on construction
/// and restores the previous context on destruction.
class ContextScope {
public:
    explicit ContextScope(NexusOperationExecutionContext* ctx) noexcept
        : previous_(NexusOperationExecutionContext::has_current()
                        ? &NexusOperationExecutionContext::current()
                        : nullptr) {
        NexusOperationExecutionContext::set_current(ctx);
    }

    ~ContextScope() noexcept {
        NexusOperationExecutionContext::set_current(previous_);
    }

    ContextScope(const ContextScope&) = delete;
    ContextScope& operator=(const ContextScope&) = delete;

private:
    NexusOperationExecutionContext* previous_;
};

// ── NexusWorkflowRunHandle ─────────────────────────────────────────────────

/// Handle referencing a workflow run backing a Nexus async operation.
/// Used to produce and parse operation tokens.
class TEMPORALIO_EXPORT NexusWorkflowRunHandle {
public:
    NexusWorkflowRunHandle(std::string ns, std::string workflow_id,
                           int version = 0);

    /// Get the namespace.
    const std::string& ns() const noexcept { return ns_; }

    /// Get the workflow ID.
    const std::string& workflow_id() const noexcept { return workflow_id_; }

    /// Get the token version.
    int version() const noexcept { return version_; }

    /// Create a handle from an operation token string (base64-encoded JSON).
    /// @throws std::invalid_argument if the token is invalid.
    static NexusWorkflowRunHandle from_token(const std::string& token);

    /// Serialize this handle to an operation token string (base64-encoded JSON).
    std::string to_token() const;

private:
    std::string ns_;
    std::string workflow_id_;
    int version_;
};

// ── INexusOperationHandler ──────────────────────────────────────────────────

/// Base interface for Nexus operation handlers.
/// WARNING: Nexus support is experimental.
class INexusOperationHandler {
public:
    virtual ~INexusOperationHandler() = default;

    /// Handle an operation start request.
    virtual coro::Task<OperationStartResult> start_async(
        OperationStartContext context, std::any input) = 0;

    /// Fetch the result of a previously started async operation.
    virtual coro::Task<std::any> fetch_result_async(
        OperationFetchResultContext context) = 0;

    /// Fetch info about a previously started async operation.
    virtual coro::Task<NexusOperationState> fetch_info_async(
        OperationFetchInfoContext context) = 0;

    /// Handle an operation cancel request.
    virtual coro::Task<void> cancel_async(
        OperationCancelContext context) = 0;

    /// Get the operation name.
    virtual const std::string& name() const = 0;
};

// ── WorkflowRunOperationContext ─────────────────────────────────────────────

/// Context used to create workflow run handles from within Nexus operation
/// start handlers. Passed to handle factory functions.
class TEMPORALIO_EXPORT WorkflowRunOperationContext {
public:
    explicit WorkflowRunOperationContext(OperationStartContext context);

    /// Get the Nexus handler context for the start call.
    const OperationStartContext& handler_context() const noexcept {
        return context_;
    }

    /// Start a workflow by type name and return a run handle.
    /// @param workflow Workflow type name.
    /// @param args Serialized workflow arguments.
    /// @param task_queue Task queue (empty to use the current task queue).
    /// @param workflow_id Workflow ID (required).
    coro::Task<NexusWorkflowRunHandle> start_workflow(
        const std::string& workflow, const std::string& args,
        const std::string& task_queue, const std::string& workflow_id);

private:
    OperationStartContext context_;
};

// ── WorkflowRunOperationHandler ─────────────────────────────────────────────

/// Operation handler backed by Temporal workflows.
///
/// Usage:
///   auto handler = WorkflowRunOperationHandler::from_handle_factory(
///       "my-operation",
///       [](WorkflowRunOperationContext& ctx, const std::string& input)
///           -> coro::Task<NexusWorkflowRunHandle> {
///           co_return co_await ctx.start_workflow(
///               "MyWorkflow", input, "", "my-workflow-id");
///       });
class TEMPORALIO_EXPORT WorkflowRunOperationHandler : public INexusOperationHandler {
public:
    /// Factory function type: (context, input) -> Task<NexusWorkflowRunHandle>.
    using HandleFactory = std::function<coro::Task<NexusWorkflowRunHandle>(
        WorkflowRunOperationContext&, const std::string&)>;

    /// Create an operation handler from the given handle factory.
    static std::unique_ptr<WorkflowRunOperationHandler> from_handle_factory(
        std::string name, HandleFactory factory);

    coro::Task<OperationStartResult> start_async(
        OperationStartContext context, std::any input) override;

    coro::Task<std::any> fetch_result_async(
        OperationFetchResultContext context) override;

    coro::Task<NexusOperationState> fetch_info_async(
        OperationFetchInfoContext context) override;

    coro::Task<void> cancel_async(OperationCancelContext context) override;

    const std::string& name() const override { return name_; }

private:
    WorkflowRunOperationHandler(std::string name, HandleFactory factory);

    std::string name_;
    HandleFactory factory_;
};

// ── NexusServiceDefinition ──────────────────────────────────────────────────

/// A Nexus service definition containing a set of operation handlers.
/// WARNING: Nexus support is experimental.
class NexusServiceDefinition {
public:
    /// Construct with name and operations.
    NexusServiceDefinition(
        std::string name,
        std::vector<std::shared_ptr<INexusOperationHandler>> operations)
        : name_(std::move(name)) {
        operations_.reserve(operations.size());
        for (auto& op : operations) {
            if (op) {
                operations_by_name_.emplace(op->name(), op);
            }
            operations_.push_back(std::move(op));
        }
    }

    /// Get the service name.
    const std::string& name() const noexcept { return name_; }

    /// Get the registered operations.
    const std::vector<std::shared_ptr<INexusOperationHandler>>& operations()
        const noexcept {
        return operations_;
    }

    /// Look up an operation by name. Returns nullptr if not found.
    /// O(1) average-case lookup.
    INexusOperationHandler* find_operation(const std::string& op_name) const {
        auto it = operations_by_name_.find(op_name);
        if (it != operations_by_name_.end()) {
            return it->second.get();
        }
        return nullptr;
    }

private:
    std::string name_;
    std::vector<std::shared_ptr<INexusOperationHandler>> operations_;
    std::unordered_map<std::string, std::shared_ptr<INexusOperationHandler>>
        operations_by_name_;
};

} // namespace temporalio::nexus
