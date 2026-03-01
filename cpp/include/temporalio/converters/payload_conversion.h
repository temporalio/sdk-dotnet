#pragma once

/// @file converters/payload_conversion.h
/// @brief Conversion utilities between the SDK's Payload/Failure structs
///        and the protobuf-generated message types.
///
/// The SDK uses simplified Payload/Failure structs internally for ease of use.
/// These utilities convert to/from the protobuf wire-format messages that are
/// exchanged with the Rust bridge layer.

#include <cstdint>
#include <string>
#include <vector>

#include <temporal/api/common/v1/message.pb.h>
#include <temporal/api/failure/v1/message.pb.h>
#include <temporalio/converters/data_converter.h>

namespace temporalio::converters {

/// Convert the SDK's Payload struct to a protobuf Payload message.
inline temporal::api::common::v1::Payload to_proto_payload(
    const Payload& payload) {
    temporal::api::common::v1::Payload proto;
    for (const auto& [key, value] : payload.metadata) {
        (*proto.mutable_metadata())[key] = value;
    }
    proto.set_data(payload.data.data(), payload.data.size());
    return proto;
}

/// Convert a protobuf Payload message to the SDK's Payload struct.
inline Payload from_proto_payload(
    const temporal::api::common::v1::Payload& proto) {
    Payload payload;
    for (const auto& [key, value] : proto.metadata()) {
        payload.metadata[key] = value;
    }
    const auto& data = proto.data();
    payload.data.assign(
        reinterpret_cast<const uint8_t*>(data.data()),
        reinterpret_cast<const uint8_t*>(data.data()) + data.size());
    return payload;
}

/// Convert a vector of SDK Payloads to a protobuf Payloads message.
inline temporal::api::common::v1::Payloads to_proto_payloads(
    const std::vector<Payload>& payloads) {
    temporal::api::common::v1::Payloads proto;
    for (const auto& p : payloads) {
        *proto.add_payloads() = to_proto_payload(p);
    }
    return proto;
}

/// Convert a protobuf Payloads message to a vector of SDK Payloads.
inline std::vector<Payload> from_proto_payloads(
    const temporal::api::common::v1::Payloads& proto) {
    std::vector<Payload> payloads;
    payloads.reserve(static_cast<size_t>(proto.payloads_size()));
    for (const auto& p : proto.payloads()) {
        payloads.push_back(from_proto_payload(p));
    }
    return payloads;
}

/// Convert the SDK's Failure struct to a protobuf Failure message.
inline temporal::api::failure::v1::Failure to_proto_failure(
    const Failure& failure) {
    temporal::api::failure::v1::Failure proto;
    proto.set_message(failure.message);
    proto.set_stack_trace(failure.stack_trace);

    if (failure.cause) {
        *proto.mutable_cause() = to_proto_failure(*failure.cause);
    }

    // Map the failure_type string to the appropriate oneof variant
    if (failure.failure_type == "ApplicationFailure") {
        auto* info = proto.mutable_application_failure_info();
        info->set_type(failure.error_type);
        info->set_non_retryable(failure.non_retryable);
        for (const auto& detail : failure.details) {
            *info->mutable_details()->add_payloads() = to_proto_payload(detail);
        }
    } else if (failure.failure_type == "TimeoutFailure") {
        proto.mutable_timeout_failure_info();
    } else if (failure.failure_type == "CanceledFailure") {
        auto* info = proto.mutable_canceled_failure_info();
        for (const auto& detail : failure.details) {
            *info->mutable_details()->add_payloads() = to_proto_payload(detail);
        }
    } else if (failure.failure_type == "TerminatedFailure") {
        proto.mutable_terminated_failure_info();
    } else if (failure.failure_type == "ServerFailure") {
        auto* info = proto.mutable_server_failure_info();
        info->set_non_retryable(failure.non_retryable);
    } else if (failure.failure_type == "ActivityFailure") {
        auto* info = proto.mutable_activity_failure_info();
        if (failure.activity_failure_info) {
            info->mutable_activity_type()->set_name(
                failure.activity_failure_info->activity_type);
            info->set_activity_id(
                failure.activity_failure_info->activity_id);
            info->set_identity(failure.activity_failure_info->identity);
            info->set_retry_state(
                static_cast<decltype(info->retry_state())>(
                    failure.activity_failure_info->retry_state));
        }
    } else if (failure.failure_type == "ChildWorkflowFailure") {
        auto* info = proto.mutable_child_workflow_execution_failure_info();
        if (failure.child_workflow_failure_info) {
            info->set_namespace_(
                failure.child_workflow_failure_info->ns);
            info->mutable_workflow_execution()->set_workflow_id(
                failure.child_workflow_failure_info->workflow_id);
            info->mutable_workflow_execution()->set_run_id(
                failure.child_workflow_failure_info->run_id);
            info->mutable_workflow_type()->set_name(
                failure.child_workflow_failure_info->workflow_type);
            info->set_retry_state(
                static_cast<decltype(info->retry_state())>(
                    failure.child_workflow_failure_info->retry_state));
        }
    } else if (failure.failure_type == "NexusOperationFailure") {
        auto* info = proto.mutable_nexus_operation_execution_failure_info();
        if (failure.nexus_operation_failure_info) {
            info->set_endpoint(
                failure.nexus_operation_failure_info->endpoint);
            info->set_service(
                failure.nexus_operation_failure_info->service);
            info->set_operation(
                failure.nexus_operation_failure_info->operation);
            info->set_operation_token(
                failure.nexus_operation_failure_info->operation_token);
            info->set_scheduled_event_id(
                failure.nexus_operation_failure_info->scheduled_event_id);
        }
    } else if (failure.failure_type == "NexusHandlerFailure") {
        proto.mutable_nexus_handler_failure_info();
    }

    if (failure.encoded_attributes.has_value()) {
        *proto.mutable_encoded_attributes() =
            to_proto_payload(*failure.encoded_attributes);
    }

    return proto;
}

/// Convert a protobuf Failure message to the SDK's Failure struct.
inline Failure from_proto_failure(
    const temporal::api::failure::v1::Failure& proto) {
    Failure failure;
    failure.message = proto.message();
    failure.stack_trace = proto.stack_trace();

    if (proto.has_cause()) {
        failure.cause =
            std::make_shared<Failure>(from_proto_failure(proto.cause()));
    }

    // Map the oneof variant to the failure_type and error_type strings
    if (proto.has_application_failure_info()) {
        const auto& info = proto.application_failure_info();
        failure.failure_type = "ApplicationFailure";
        failure.error_type = info.type();
        failure.non_retryable = info.non_retryable();
        if (info.has_details()) {
            failure.details = from_proto_payloads(info.details());
        }
    } else if (proto.has_timeout_failure_info()) {
        failure.failure_type = "TimeoutFailure";
    } else if (proto.has_canceled_failure_info()) {
        failure.failure_type = "CanceledFailure";
        const auto& info = proto.canceled_failure_info();
        if (info.has_details()) {
            failure.details = from_proto_payloads(info.details());
        }
    } else if (proto.has_terminated_failure_info()) {
        failure.failure_type = "TerminatedFailure";
    } else if (proto.has_server_failure_info()) {
        failure.failure_type = "ServerFailure";
        failure.non_retryable = proto.server_failure_info().non_retryable();
    } else if (proto.has_activity_failure_info()) {
        failure.failure_type = "ActivityFailure";
        const auto& info = proto.activity_failure_info();
        Failure::ActivityFailureInfo afi;
        afi.activity_type = info.activity_type().name();
        afi.activity_id = info.activity_id();
        afi.identity = info.identity();
        afi.retry_state = static_cast<int>(info.retry_state());
        failure.activity_failure_info = std::move(afi);
    } else if (proto.has_child_workflow_execution_failure_info()) {
        failure.failure_type = "ChildWorkflowFailure";
        const auto& info = proto.child_workflow_execution_failure_info();
        Failure::ChildWorkflowFailureInfo cwfi;
        cwfi.ns = info.namespace_();
        cwfi.workflow_id = info.workflow_execution().workflow_id();
        cwfi.run_id = info.workflow_execution().run_id();
        cwfi.workflow_type = info.workflow_type().name();
        cwfi.retry_state = static_cast<int>(info.retry_state());
        failure.child_workflow_failure_info = std::move(cwfi);
    } else if (proto.has_nexus_operation_execution_failure_info()) {
        failure.failure_type = "NexusOperationFailure";
        const auto& info = proto.nexus_operation_execution_failure_info();
        Failure::NexusOperationFailureInfo nofi;
        nofi.endpoint = info.endpoint();
        nofi.service = info.service();
        nofi.operation = info.operation();
        nofi.operation_token = info.operation_token();
        nofi.scheduled_event_id = info.scheduled_event_id();
        failure.nexus_operation_failure_info = std::move(nofi);
    } else if (proto.has_nexus_handler_failure_info()) {
        failure.failure_type = "NexusHandlerFailure";
    } else {
        failure.failure_type = "Failure";
    }

    if (proto.has_encoded_attributes()) {
        failure.encoded_attributes =
            from_proto_payload(proto.encoded_attributes());
    }

    return failure;
}

/// Serialize an SDK Payload struct to protobuf bytes.
inline std::vector<uint8_t> serialize_payload(const Payload& payload) {
    auto proto = to_proto_payload(payload);
    std::string bytes;
    proto.SerializeToString(&bytes);
    return std::vector<uint8_t>(bytes.begin(), bytes.end());
}

/// Deserialize protobuf bytes into an SDK Payload struct.
inline Payload deserialize_payload(const std::vector<uint8_t>& data) {
    temporal::api::common::v1::Payload proto;
    proto.ParseFromArray(data.data(), static_cast<int>(data.size()));
    return from_proto_payload(proto);
}

/// Serialize an SDK Failure struct to protobuf bytes.
inline std::vector<uint8_t> serialize_failure(const Failure& failure) {
    auto proto = to_proto_failure(failure);
    std::string bytes;
    proto.SerializeToString(&bytes);
    return std::vector<uint8_t>(bytes.begin(), bytes.end());
}

/// Deserialize protobuf bytes into an SDK Failure struct.
inline Failure deserialize_failure(const std::vector<uint8_t>& data) {
    temporal::api::failure::v1::Failure proto;
    proto.ParseFromArray(data.data(), static_cast<int>(data.size()));
    return from_proto_failure(proto);
}

}  // namespace temporalio::converters
