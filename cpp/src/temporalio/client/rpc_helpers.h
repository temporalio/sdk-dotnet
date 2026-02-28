#pragma once

/// @file rpc_helpers.h
/// @brief Internal helpers for RPC calls and protobuf payload conversion.
/// Shared between temporal_client.cpp and workflow_handle.cpp.

#include <temporalio/coro/task_completion_source.h>
#include <temporalio/converters/data_converter.h>
#include <temporalio/exceptions/temporal_exception.h>

#include "temporalio/bridge/client.h"

#include <temporal/api/common/v1/message.pb.h>

#include <cstdint>
#include <optional>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

namespace temporalio::client::internal {

/// Bridge the callback-based rpc_call_async to a coroutine.
/// Returns the raw response bytes on success, throws RpcException on failure.
inline coro::Task<std::vector<uint8_t>> rpc_call(
    bridge::Client& client, bridge::RpcCallOptions opts) {
    auto tcs =
        std::make_shared<coro::TaskCompletionSource<std::vector<uint8_t>>>();

    client.rpc_call_async(
        opts,
        [tcs](std::optional<bridge::RpcCallResult> result,
              std::optional<bridge::RpcCallError> error) {
            if (error) {
                tcs->try_set_exception(std::make_exception_ptr(
                    exceptions::RpcException(
                        static_cast<exceptions::RpcException::StatusCode>(
                            error->status_code),
                        error->message, error->details)));
            } else if (result) {
                tcs->try_set_result(std::move(result->response));
            } else {
                tcs->try_set_exception(std::make_exception_ptr(
                    std::runtime_error("RPC call returned no result")));
            }
        });

    co_return co_await tcs->task();
}

/// Convert a protobuf Payload to a converters::Payload.
inline converters::Payload from_proto_payload(
    const temporal::api::common::v1::Payload& proto_payload) {
    converters::Payload payload;
    for (const auto& [key, value] : proto_payload.metadata()) {
        payload.metadata[key] = value;
    }
    payload.data.assign(proto_payload.data().begin(),
                        proto_payload.data().end());
    return payload;
}

/// Convert a converters::Payload to a protobuf Payload.
inline void set_proto_payload(
    temporal::api::common::v1::Payload* proto_payload,
    const converters::Payload& payload) {
    for (const auto& [key, value] : payload.metadata) {
        (*proto_payload->mutable_metadata())[key] = value;
    }
    proto_payload->set_data(payload.data.data(), payload.data.size());
}

/// Add a vector of converters::Payload to a protobuf Payloads field.
inline void set_proto_payloads(
    temporal::api::common::v1::Payloads* proto_payloads,
    const std::vector<converters::Payload>& payloads) {
    for (const auto& p : payloads) {
        set_proto_payload(proto_payloads->add_payloads(), p);
    }
}

}  // namespace temporalio::client::internal
