#include "temporalio/nexus/operation_handler.h"
#include "temporalio/client/temporal_client.h"
#include "temporalio/client/workflow_handle.h"

#include <nlohmann/json.hpp>
#include <stdexcept>
#include <utility>
#include <vector>

// Base64 encode/decode helpers
namespace {

const char kBase64Chars[] =
    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

std::string base64_encode(const std::string& input) {
    std::string result;
    result.reserve(((input.size() + 2) / 3) * 4);
    int val = 0;
    int valb = -6;
    for (char ch : input) {
        auto c = static_cast<unsigned char>(ch);
        val = (val << 8) + c;
        valb += 8;
        while (valb >= 0) {
            result.push_back(kBase64Chars[(val >> valb) & 0x3F]);
            valb -= 6;
        }
    }
    if (valb > -6) {
        result.push_back(kBase64Chars[((val << 8) >> (valb + 8)) & 0x3F]);
    }
    while (result.size() % 4 != 0) {
        result.push_back('=');
    }
    return result;
}

std::string base64_decode(const std::string& input) {
    // Build decode table
    std::vector<int> table(256, -1);
    for (int i = 0; i < 64; ++i) {
        table[static_cast<unsigned char>(kBase64Chars[i])] = i;
    }

    std::string result;
    int val = 0;
    int valb = -8;
    for (char ch : input) {
        auto c = static_cast<unsigned char>(ch);
        if (c == '=') {
            break;
        }
        if (table[c] == -1) {
            throw std::invalid_argument("Invalid base64 character");
        }
        val = (val << 6) + table[c];
        valb += 6;
        if (valb >= 0) {
            result.push_back(static_cast<char>((val >> valb) & 0xFF));
            valb -= 8;
        }
    }
    return result;
}

// Thread-local context pointer
thread_local temporalio::nexus::NexusOperationExecutionContext*
    tl_current_context = nullptr;

} // namespace

namespace temporalio::nexus {

// ── NexusOperationExecutionContext ──────────────────────────────────────────

NexusOperationExecutionContext::NexusOperationExecutionContext(
    OperationContext handler_context, NexusOperationInfo info,
    std::shared_ptr<client::TemporalClient> client)
    : handler_context_(std::move(handler_context)),
      info_(std::move(info)),
      client_(std::move(client)) {}

client::TemporalClient& NexusOperationExecutionContext::temporal_client() const {
    if (!client_) {
        throw std::logic_error(
            "No Temporal client available. This could either be a test "
            "environment without a client set, or the worker was created "
            "in an advanced way without a TemporalClient instance.");
    }
    return *client_;
}

NexusOperationExecutionContext& NexusOperationExecutionContext::current() {
    if (!tl_current_context) {
        throw std::logic_error("No current Nexus operation context");
    }
    return *tl_current_context;
}

bool NexusOperationExecutionContext::has_current() noexcept {
    return tl_current_context != nullptr;
}

void NexusOperationExecutionContext::set_current(
    NexusOperationExecutionContext* ctx) noexcept {
    tl_current_context = ctx;
}

// ── NexusWorkflowRunHandle ─────────────────────────────────────────────────

NexusWorkflowRunHandle::NexusWorkflowRunHandle(std::string ns,
                                               std::string workflow_id,
                                               int version)
    : ns_(std::move(ns)),
      workflow_id_(std::move(workflow_id)),
      version_(version) {}

NexusWorkflowRunHandle NexusWorkflowRunHandle::from_token(
    const std::string& token) {
    std::string decoded;
    try {
        decoded = base64_decode(token);
    } catch (const std::invalid_argument&) {
        throw std::invalid_argument("Token invalid: bad base64");
    }

    auto j = nlohmann::json::parse(decoded, nullptr, false);
    if (j.is_discarded() || !j.is_object()) {
        throw std::invalid_argument("Token invalid: bad JSON");
    }

    if (!j.contains("ns") || !j["ns"].is_string()) {
        throw std::invalid_argument("Token invalid: missing namespace");
    }
    if (!j.contains("wid") || !j["wid"].is_string()) {
        throw std::invalid_argument("Token invalid: missing workflow ID");
    }

    int version = 0;
    if (j.contains("v") && !j["v"].is_null()) {
        version = j["v"].get<int>();
        if (version != 0) {
            throw std::invalid_argument("Unsupported token version: " +
                                        std::to_string(version));
        }
    }

    return NexusWorkflowRunHandle(j["ns"].get<std::string>(),
                                  j["wid"].get<std::string>(), version);
}

std::string NexusWorkflowRunHandle::to_token() const {
    nlohmann::json j;
    j["ns"] = ns_;
    j["wid"] = workflow_id_;
    if (version_ != 0) {
        j["v"] = version_;
    }
    j["t"] = 1; // type = workflow
    return base64_encode(j.dump());
}

// ── WorkflowRunOperationContext ─────────────────────────────────────────────

WorkflowRunOperationContext::WorkflowRunOperationContext(
    OperationStartContext context)
    : context_(std::move(context)) {}

coro::Task<NexusWorkflowRunHandle>
WorkflowRunOperationContext::start_workflow(const std::string& workflow,
                                           const std::string& args,
                                           const std::string& task_queue,
                                           const std::string& workflow_id) {
    auto& exec_ctx = NexusOperationExecutionContext::current();
    auto& client = exec_ctx.temporal_client();

    // Determine task queue: use provided or fall back to current
    std::string effective_task_queue = task_queue;
    if (effective_task_queue.empty()) {
        effective_task_queue = exec_ctx.info().task_queue;
    }

    // Build workflow options
    client::WorkflowOptions options;
    options.id = workflow_id;
    options.task_queue = effective_task_queue;
    if (context_.request_id) {
        options.request_id = *context_.request_id;
    }

    // Create the handle before starting so we can encode the token.
    // Namespace always comes from the execution context info.
    NexusWorkflowRunHandle handle(exec_ctx.info().ns, workflow_id, 0);

    // TODO: Start the workflow via the client and set up outbound links,
    // callbacks, etc. when the full client integration is wired up.
    co_await client.start_workflow(workflow, options, args);

    co_return handle;
}

// ── WorkflowRunOperationHandler ─────────────────────────────────────────────

WorkflowRunOperationHandler::WorkflowRunOperationHandler(std::string name,
                                                         HandleFactory factory)
    : name_(std::move(name)), factory_(std::move(factory)) {}

std::unique_ptr<WorkflowRunOperationHandler>
WorkflowRunOperationHandler::from_handle_factory(std::string name,
                                                 HandleFactory factory) {
    return std::unique_ptr<WorkflowRunOperationHandler>(
        new WorkflowRunOperationHandler(std::move(name), std::move(factory)));
}

coro::Task<OperationStartResult> WorkflowRunOperationHandler::start_async(
    OperationStartContext context, std::any input) {
    // Extract input as string if possible
    std::string input_str;
    if (input.has_value() && input.type() == typeid(std::string)) {
        input_str = std::any_cast<std::string>(std::move(input));
    }

    WorkflowRunOperationContext op_ctx(std::move(context));
    auto handle = co_await factory_(op_ctx, input_str);
    co_return OperationStartResult::async_result(handle.to_token());
}

coro::Task<std::any> WorkflowRunOperationHandler::fetch_result_async(
    OperationFetchResultContext context) {
    // Parse the token to get the workflow handle
    NexusWorkflowRunHandle run_handle =
        NexusWorkflowRunHandle::from_token(context.operation_token);

    auto& exec_ctx = NexusOperationExecutionContext::current();

    // Verify namespace matches
    if (run_handle.ns() != exec_ctx.info().ns) {
        throw std::invalid_argument("Invalid namespace in operation token");
    }

    // Get the workflow handle and wait for result
    auto wf_handle = exec_ctx.temporal_client().get_workflow_handle(
        run_handle.workflow_id());
    auto result_payload = co_await wf_handle.get_result_payload();

    // Return the raw payload data as the result
    co_return std::any(
        std::string(result_payload.data.begin(), result_payload.data.end()));
}

coro::Task<NexusOperationState>
WorkflowRunOperationHandler::fetch_info_async(
    OperationFetchInfoContext context) {
    // Parse the token to get the workflow handle
    NexusWorkflowRunHandle run_handle =
        NexusWorkflowRunHandle::from_token(context.operation_token);

    auto& exec_ctx = NexusOperationExecutionContext::current();

    // Verify namespace matches
    if (run_handle.ns() != exec_ctx.info().ns) {
        throw std::invalid_argument("Invalid namespace in operation token");
    }

    // Describe the workflow to get its current status
    auto wf_handle = exec_ctx.temporal_client().get_workflow_handle(
        run_handle.workflow_id());
    auto desc = co_await wf_handle.describe();

    // Map workflow execution status to a Nexus operation state string
    std::string state;
    switch (desc.status) {
        case common::WorkflowExecutionStatus::kRunning:
            state = "RUNNING";
            break;
        case common::WorkflowExecutionStatus::kCompleted:
            state = "SUCCEEDED";
            break;
        case common::WorkflowExecutionStatus::kFailed:
            state = "FAILED";
            break;
        case common::WorkflowExecutionStatus::kCanceled:
            state = "CANCELED";
            break;
        case common::WorkflowExecutionStatus::kTerminated:
            state = "FAILED";
            break;
        case common::WorkflowExecutionStatus::kTimedOut:
            state = "FAILED";
            break;
        case common::WorkflowExecutionStatus::kContinuedAsNew:
            state = "RUNNING";
            break;
        default:
            state = "UNKNOWN";
            break;
    }

    co_return NexusOperationState{std::move(state)};
}

coro::Task<void> WorkflowRunOperationHandler::cancel_async(
    OperationCancelContext context) {
    // Parse the token to get the workflow handle
    NexusWorkflowRunHandle handle =
        NexusWorkflowRunHandle::from_token(context.operation_token);

    auto& exec_ctx = NexusOperationExecutionContext::current();

    // Verify namespace matches
    if (handle.ns() != exec_ctx.info().ns) {
        throw std::invalid_argument("Invalid namespace in operation token");
    }

    // Cancel the workflow
    auto wf_handle =
        exec_ctx.temporal_client().get_workflow_handle(handle.workflow_id());
    co_await wf_handle.cancel();
}

} // namespace temporalio::nexus
