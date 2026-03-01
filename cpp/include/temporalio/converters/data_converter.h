#pragma once

/// @file Data conversion between user types and Temporal payloads.

#include <temporalio/export.h>

#include <any>
#include <cstdint>
#include <exception>
#include <functional>
#include <memory>
#include <optional>
#include <set>
#include <stdexcept>
#include <string>
#include <string_view>
#include <typeindex>
#include <typeinfo>
#include <unordered_map>
#include <vector>

namespace temporalio::converters {

/// Represents a Temporal Payload (encoding metadata + data bytes).
/// This is a simplified representation; full protobuf Payload will be used
/// when the proto types are generated. For now, this provides the core
/// abstraction.
struct Payload {
    /// Metadata key-value pairs (e.g., "encoding" -> "json/plain").
    std::unordered_map<std::string, std::string> metadata;
    /// Raw payload data bytes.
    std::vector<uint8_t> data;
};

/// Represents a Temporal Failure — the wire format for exception information.
/// This is a simplified C++ representation of the temporal.api.failure.v1.Failure
/// protobuf message. When full proto types are integrated, this may become a
/// thin wrapper around the generated type.
struct Failure {
    /// Human-readable error message.
    std::string message;
    /// The failure kind string — identifies which oneof variant this maps to.
    /// Values: "ApplicationFailure", "CanceledFailure", "TerminatedFailure",
    /// "TimeoutFailure", "ServerFailure", "ActivityFailure",
    /// "ChildWorkflowFailure", "NexusOperationFailure", "NexusHandlerFailure",
    /// or "Failure" (generic).
    std::string failure_type;
    /// For ApplicationFailure: the user-defined error type string (e.g.,
    /// "MyCustomError"). Maps to ApplicationFailureInfo.type in protobuf.
    /// Empty string if no custom error type was specified.
    std::string error_type;
    /// Stack trace string, if available.
    std::string stack_trace;
    /// Optional inner cause (recursive failure chain).
    std::shared_ptr<Failure> cause;
    /// Encoded attributes payload (used when encode_common_attributes is set).
    std::optional<Payload> encoded_attributes;
    /// Whether this failure is non-retryable.
    bool non_retryable = false;
    /// Application error category, if any.
    std::string category;
    /// Detail payloads for application/canceled failures.
    std::vector<Payload> details;

    /// Type-specific fields for ActivityFailure.
    struct ActivityFailureInfo {
        std::string activity_type;
        std::string activity_id;
        std::string identity;
        int retry_state{0};
    };
    std::optional<ActivityFailureInfo> activity_failure_info;

    /// Type-specific fields for ChildWorkflowFailure.
    struct ChildWorkflowFailureInfo {
        std::string ns;
        std::string workflow_id;
        std::string run_id;
        std::string workflow_type;
        int retry_state{0};
    };
    std::optional<ChildWorkflowFailureInfo> child_workflow_failure_info;

    /// Type-specific fields for NexusOperationFailure.
    struct NexusOperationFailureInfo {
        std::string endpoint;
        std::string service;
        std::string operation;
        std::string operation_token;
        int64_t scheduled_event_id{0};
    };
    std::optional<NexusOperationFailureInfo> nexus_operation_failure_info;
};

/// Represents a raw, unconverted payload value.
class RawValue {
public:
    explicit RawValue(Payload payload) : payload_(std::move(payload)) {}

    const Payload& payload() const noexcept { return payload_; }
    Payload& payload() noexcept { return payload_; }

private:
    Payload payload_;
};

/// Interface for a specific encoding converter.
/// Each encoding converter handles one encoding type (e.g., "json/plain",
/// "binary/null", "binary/plain", "json/protobuf", "binary/protobuf").
class IEncodingConverter {
public:
    virtual ~IEncodingConverter() = default;

    /// The encoding name (placed in payload metadata "encoding" key).
    virtual std::string_view encoding() const = 0;

    /// Try to convert the given value to a payload. Returns nullopt if this
    /// converter cannot handle the value and the next should be tried.
    virtual std::optional<Payload> try_to_payload(
        const std::any& value) const = 0;

    /// Convert the given payload to a value of the specified type.
    /// Only called for payloads whose encoding matches this converter.
    virtual std::any to_value(const Payload& payload,
                              std::type_index type) const = 0;
};

/// Interface for converting values to/from Temporal payloads.
/// Deterministic, synchronous -- used in workflows.
class IPayloadConverter {
public:
    virtual ~IPayloadConverter() = default;

    /// Convert the given value to a payload.
    virtual Payload to_payload(const std::any& value) const = 0;

    /// Convert the given payload to a value of the specified type.
    virtual std::any to_value(const Payload& payload,
                              std::type_index type) const = 0;

    /// Convenience template: convert a typed value to a payload.
    template <typename T>
    Payload to_payload_typed(const T& value) const {
        return to_payload(std::any(value));
    }

    /// Convenience template: convert a payload to a typed value.
    template <typename T>
    T to_value_typed(const Payload& payload) const {
        return std::any_cast<T>(to_value(payload, std::type_index(typeid(T))));
    }
};

/// Interface for converting exceptions to/from Temporal Failure
/// representations. Deterministic, synchronous -- used in workflows.
class IFailureConverter {
public:
    virtual ~IFailureConverter() = default;

    /// Convert an exception to a Failure struct.
    virtual Failure to_failure(std::exception_ptr exception,
                               const IPayloadConverter& converter) const = 0;

    /// Convert a Failure struct to an exception.
    virtual std::exception_ptr to_exception(
        const Failure& failure,
        const IPayloadConverter& converter) const = 0;
};

/// Interface for encoding/decoding payloads (e.g., encryption, compression).
class IPayloadCodec {
public:
    virtual ~IPayloadCodec() = default;

    /// Encode the given payloads.
    virtual std::vector<Payload> encode(
        std::vector<Payload> payloads) const = 0;

    /// Decode the given payloads.
    virtual std::vector<Payload> decode(
        std::vector<Payload> payloads) const = 0;
};

/// Default payload converter that iterates over encoding converters.
/// Tries each converter in order for to_payload; looks up by encoding for
/// to_value.
class TEMPORALIO_EXPORT DefaultPayloadConverter : public IPayloadConverter {
public:
    /// Constructs with the standard encoding converter set:
    /// BinaryNull, BinaryPlain, JsonPlain.
    /// (JsonProto and BinaryProto added when proto support is integrated.)
    DefaultPayloadConverter();

    /// Constructs with a custom set of encoding converters.
    explicit DefaultPayloadConverter(
        std::vector<std::unique_ptr<IEncodingConverter>> converters);

    Payload to_payload(const std::any& value) const override;
    std::any to_value(const Payload& payload,
                      std::type_index type) const override;

    /// Access the encoding converters.
    const std::vector<std::unique_ptr<IEncodingConverter>>& encoding_converters()
        const noexcept {
        return converters_;
    }

private:
    std::vector<std::unique_ptr<IEncodingConverter>> converters_;
    std::unordered_map<std::string, IEncodingConverter*> indexed_;
};

/// Options for the default failure converter.
struct DefaultFailureConverterOptions {
    /// If true, move message and stack trace to encoded attributes
    /// (subject to codecs, useful for encryption).
    bool encode_common_attributes = false;
};

/// Default failure converter implementation.
/// Maps the full C++ exception hierarchy to/from Failure structs.
class TEMPORALIO_EXPORT DefaultFailureConverter : public IFailureConverter {
public:
    DefaultFailureConverter();
    explicit DefaultFailureConverter(DefaultFailureConverterOptions options);

    Failure to_failure(std::exception_ptr exception,
                       const IPayloadConverter& converter) const override;

    std::exception_ptr to_exception(
        const Failure& failure,
        const IPayloadConverter& converter) const override;

    const DefaultFailureConverterOptions& options() const noexcept {
        return options_;
    }

private:
    DefaultFailureConverterOptions options_;
};

/// Data converter which combines a payload converter, a failure converter,
/// and an optional payload codec.
struct TEMPORALIO_EXPORT DataConverter {
    std::shared_ptr<IPayloadConverter> payload_converter;
    std::shared_ptr<IFailureConverter> failure_converter;
    std::shared_ptr<IPayloadCodec> payload_codec;  // may be null

    /// Returns the default data converter instance.
    static DataConverter default_instance();
};

// -- Built-in encoding converters --

/// Handles null/empty values with "binary/null" encoding.
class TEMPORALIO_EXPORT BinaryNullConverter : public IEncodingConverter {
public:
    std::string_view encoding() const override;
    std::optional<Payload> try_to_payload(const std::any& value) const override;
    std::any to_value(const Payload& payload,
                      std::type_index type) const override;
};

/// Handles raw byte vectors with "binary/plain" encoding.
class TEMPORALIO_EXPORT BinaryPlainConverter : public IEncodingConverter {
public:
    std::string_view encoding() const override;
    std::optional<Payload> try_to_payload(const std::any& value) const override;
    std::any to_value(const Payload& payload,
                      std::type_index type) const override;
};

/// Handles JSON serialization with "json/plain" encoding.
/// Uses nlohmann/json internally.
///
/// Built-in types: std::string, bool, int, int64_t, uint64_t, float, double,
/// and nlohmann::json. Custom types can be registered with register_type()
/// or by providing nlohmann/json ADL to_json/from_json hooks.
class TEMPORALIO_EXPORT JsonPlainConverter : public IEncodingConverter {
public:
    std::string_view encoding() const override;
    std::optional<Payload> try_to_payload(const std::any& value) const override;
    std::any to_value(const Payload& payload,
                      std::type_index type) const override;

    /// Register a type for JSON serialization.
    /// The serializer converts a std::any containing T to JSON bytes.
    /// The deserializer converts JSON bytes to a std::any containing T.
    template <typename T>
    void register_type(
        std::function<std::vector<uint8_t>(const T&)> serializer,
        std::function<T(const std::vector<uint8_t>&)> deserializer) {
        auto type_idx = std::type_index(typeid(T));
        serializers_[type_idx] = [ser = std::move(serializer)](
                                     const std::any& val) {
            return ser(std::any_cast<const T&>(val));
        };
        deserializers_[type_idx] = [deser = std::move(deserializer)](
                                       const std::vector<uint8_t>& data) {
            return std::any(deser(data));
        };
        registered_types_.insert(type_idx);
    }

    /// Check if a type is registered for JSON conversion.
    bool has_type(std::type_index type) const {
        return registered_types_.count(type) > 0;
    }

private:
    std::unordered_map<std::type_index,
                       std::function<std::vector<uint8_t>(const std::any&)>>
        serializers_;
    std::unordered_map<std::type_index,
                       std::function<std::any(const std::vector<uint8_t>&)>>
        deserializers_;
    std::set<std::type_index> registered_types_;
};

/// Decode a std::any that may contain either a Payload (from protobuf
/// deserialization) or a direct typed value (from tests or internal usage).
/// If it contains a Payload, uses the given converter (or a default
/// DefaultPayloadConverter) to decode to the target type T.
/// If it already contains T, returns via std::any_cast.
template <typename T>
T decode_payload_value(const std::any& val,
                       const IPayloadConverter* converter = nullptr) {
    if (val.type() == typeid(Payload)) {
        const auto& payload = std::any_cast<const Payload&>(val);
        if (converter) {
            return std::any_cast<T>(
                converter->to_value(payload, std::type_index(typeid(T))));
        }
        DefaultPayloadConverter dc;
        return std::any_cast<T>(
            dc.to_value(payload, std::type_index(typeid(T))));
    }
    return std::any_cast<T>(val);
}

}  // namespace temporalio::converters
