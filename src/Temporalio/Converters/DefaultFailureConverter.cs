using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using NexusRpc.Handlers;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.Failure.V1;
using Temporalio.Exceptions;

namespace Temporalio.Converters
{
    /// <summary>
    /// Default implementation of <see cref="IFailureConverter" />.
    /// </summary>
    public class DefaultFailureConverter : IFailureConverter
    {
        private static readonly JsonParser ProtoJsonParser =
            new(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultFailureConverter"/> class.
        /// </summary>
        public DefaultFailureConverter()
            : this(new())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultFailureConverter"/> class.
        /// </summary>
        /// <param name="options">Options for the failure converter.</param>
        /// <remarks>
        /// This is protected because payload converters are referenced as class types, not
        /// instances, so only subclasses would call this.
        /// </remarks>
        protected DefaultFailureConverter(DefaultFailureConverterOptions options) =>
            Options = options;

        /// <summary>
        /// Gets the options this converter was created with.
        /// </summary>
        /// <remarks>
        /// Callers should never mutate this. Rather they should subclass the failure converter and
        /// pass a different value into the protected constructor.
        /// </remarks>
        public DefaultFailureConverterOptions Options { get; private init; }

        /// <inheritdoc />
        public virtual Failure ToFailure(Exception exception, IPayloadConverter payloadConverter)
        {
            Failure failure;

            // Handle Nexus HandlerException before FailureException check, since
            // HandlerException extends Exception not FailureException
            if (exception is HandlerException handlerException)
            {
                failure = CreateFailureFromHandlerException(
                    handlerException, payloadConverter);
            }
            else
            {
                // If the exception is not already a failure exception, make it an application
                // exception
                var failureEx =
                    exception as FailureException
                    ?? new ApplicationFailureException(
                        exception.Message,
                        exception.InnerException,
                        exception.GetType().Name);

                // Create new failure object. This means if it's already set we copy it. This is
                // costly, but we prefer it over potentially mutating the failure on the existing
                // exception. We pass in the stack trace from the original exception always just
                // in case it was converted to an application failure which won't have the original
                // stack trace.
                failure = CreateFailureFromException(
                    failureEx,
                    exception.StackTrace,
                    payloadConverter);
            }

            // If requested, move message and stack trace to encoded attributes.
            // Skip for round-trip failures since the message/stack_trace are already
            // part of the restored proto from the serialized details.
            if (Options.EncodeCommonAttributes)
            {
                failure.EncodedAttributes = payloadConverter.ToPayload(
                    new Dictionary<string, string>
                    {
                        ["message"] = failure.Message,
                        ["stack_trace"] = failure.StackTrace,
                    });
                failure.Message = "Encoded failure";
                failure.StackTrace = string.Empty;
            }
            return failure;
        }

        /// <inheritdoc />
        public virtual Exception ToException(Failure failure, IPayloadConverter payloadConverter)
        {
            // If encoded attributes are present and we can decode the attributes, set them as
            // expected
            if (failure.EncodedAttributes != null)
            {
#pragma warning disable CA1031 // We intentionally catch any error converting to our attrs
                try
                {
                    var attrs = payloadConverter.ToValue<Dictionary<string, string>>(
                        failure.EncodedAttributes)!;
                    if (attrs.TryGetValue("message", out string? message))
                    {
                        failure.Message = message;
                        if (attrs.TryGetValue("stack_trace", out string? stackTrace))
                        {
                            failure.StackTrace = stackTrace;
                        }
                    }
                }
                catch (Exception)
                {
                    // Do nothing
                }
#pragma warning restore CA1031
            }

            // Convert
            var inner = failure.Cause != null ? ToException(failure.Cause, payloadConverter) : null;
            switch (failure.FailureInfoCase)
            {
                case Failure.FailureInfoOneofCase.ApplicationFailureInfo:
                    return new ApplicationFailureException(failure, inner, payloadConverter);
                case Failure.FailureInfoOneofCase.TimeoutFailureInfo:
                    return new TimeoutFailureException(failure, inner, payloadConverter);
                case Failure.FailureInfoOneofCase.CanceledFailureInfo:
                    return new CanceledFailureException(failure, inner, payloadConverter);
                case Failure.FailureInfoOneofCase.TerminatedFailureInfo:
                    return new TerminatedFailureException(failure, inner, null);
                case Failure.FailureInfoOneofCase.ActivityFailureInfo:
                    return new ActivityFailureException(failure, inner);
                case Failure.FailureInfoOneofCase.ChildWorkflowExecutionFailureInfo:
                    return new ChildWorkflowFailureException(failure, inner);
                case Failure.FailureInfoOneofCase.NexusOperationExecutionFailureInfo:
                    return new NexusOperationFailureException(failure, inner);
                case Failure.FailureInfoOneofCase.NexusHandlerFailureInfo:
                    var handlerInfo = failure.NexusHandlerFailureInfo;
                    HandlerErrorRetryBehavior retryBehavior = handlerInfo.RetryBehavior switch
                    {
                        NexusHandlerErrorRetryBehavior.Retryable => HandlerErrorRetryBehavior.Retryable,
                        NexusHandlerErrorRetryBehavior.NonRetryable => HandlerErrorRetryBehavior.NonRetryable,
                        _ => HandlerErrorRetryBehavior.Unspecified,
                    };
                    return new HandlerException(
                        handlerInfo.Type,
                        failure.Message,
                        inner,
                        retryBehavior,
                        string.IsNullOrEmpty(failure.StackTrace) ? null : failure.StackTrace,
                        TemporalFailureToNexusFailure(failure));
                default:
                    return new FailureException(failure, inner);
            }
        }

        /// <summary>
        /// Convert a NexusRpc.FailureInfo back to a Temporal Failure proto. This handles
        /// round-tripping failures that were originally Temporal Failure protos serialized
        /// as NexusRpc FailureInfos.
        /// </summary>
        /// <param name="failureInfo">The NexusRpc FailureInfo to convert.</param>
        /// <param name="nonRetryable">Whether the failure is non-retryable (used for
        /// non-Temporal fallback path).</param>
        /// <returns>The reconstructed Temporal Failure proto.</returns>
        internal static Failure NexusFailureToTemporalFailure(
            NexusRpc.FailureInfo failureInfo, bool nonRetryable = true)
        {
            var apiFailure = new Failure();

            // Check if this was originally a Temporal Failure proto
            if (failureInfo.Metadata != null &&
                failureInfo.Metadata.TryGetValue("type", out var metadataType) &&
                metadataType == Failure.Descriptor.FullName)
            {
                // Details contains a JSON-serialized Temporal failure
                if (!string.IsNullOrEmpty(failureInfo.Details))
                {
                    apiFailure = ProtoJsonParser.Parse<Failure>(failureInfo.Details);
                }
            }
            else
            {
                // Create an ApplicationFailure with the Nexus failure data
                var appFailureInfo = new ApplicationFailureInfo()
                {
                    Type = "NexusFailure",
                    NonRetryable = nonRetryable,
                };
                var payloads = NexusFailureInfoToPayloads(failureInfo);
                if (payloads != null)
                {
                    appFailureInfo.Details = payloads;
                }

                apiFailure.ApplicationFailureInfo = appFailureInfo;
            }

            // Ensure these always get written
            apiFailure.Message = failureInfo.Message;
            if (!string.IsNullOrEmpty(failureInfo.StackTrace))
            {
                apiFailure.StackTrace = failureInfo.StackTrace;
            }

            return apiFailure;
        }

        /// <summary>
        /// Convert a Temporal Failure proto to a NexusRpc.FailureInfo. This is the reverse of
        /// <see cref="NexusFailureToTemporalFailure"/>. Used when deserializing
        /// NexusHandlerFailureInfo to set OriginalFailure on HandlerException for
        /// round-tripping.
        /// </summary>
        /// <param name="failure">The Temporal Failure proto to convert.</param>
        /// <returns>The NexusRpc FailureInfo for round-tripping.</returns>
        internal static NexusRpc.FailureInfo TemporalFailureToNexusFailure(Failure failure)
        {
            string detailsJson;
            try
            {
                var stripped = failure.Clone();
                stripped.Message = string.Empty;
                stripped.StackTrace = string.Empty;
                detailsJson = JsonFormatter.Default.Format(stripped);
            }
            catch (InvalidProtocolBufferException e)
            {
                return new NexusRpc.FailureInfo(
                    message: "Failed to serialize failure details",
                    details: e.Message);
            }

            return new NexusRpc.FailureInfo(
                message: failure.Message,
                metadata: new Dictionary<string, string>
                {
                    ["type"] = Failure.Descriptor.FullName,
                },
                details: detailsJson,
                stackTrace: string.IsNullOrEmpty(failure.StackTrace) ? null : failure.StackTrace);
        }

        /// <summary>
        /// Convert a NexusRpc.FailureInfo to Payloads for the non-Temporal failure path.
        /// Serializes the entire FailureInfo (with message cleared) as a JSON payload.
        /// </summary>
        private static Payloads? NexusFailureInfoToPayloads(NexusRpc.FailureInfo failureInfo)
        {
            if ((failureInfo.Metadata == null || failureInfo.Metadata.Count == 0) &&
                string.IsNullOrEmpty(failureInfo.Details))
            {
                return null;
            }

            // Create a copy without the message before serializing
            var copy = new NexusRpc.FailureInfo(
                string.Empty,
                failureInfo.Metadata,
                failureInfo.Details,
                failureInfo.StackTrace,
                failureInfo.Cause);
            var json = JsonSerializer.Serialize(copy);
            return new Payloads
            {
                Payloads_ =
                {
                    new Payload()
                    {
                        Metadata = { ["encoding"] = ByteString.CopyFromUtf8("json/plain") },
                        Data = ByteString.CopyFromUtf8(json),
                    },
                },
            };
        }

        private Failure CreateFailureFromException(
            FailureException exc,
            string? stackTrace,
            IPayloadConverter conv)
        {
            // Copy existing failure if already there
            if (exc.Failure != null)
            {
                return new(exc.Failure);
            }
            var failure = new Failure()
            {
                Message = exc.Message,
                StackTrace = stackTrace ?? string.Empty,
                Cause = exc.InnerException == null ? null : ToFailure(exc.InnerException, conv),
            };
            switch (exc)
            {
                case ApplicationFailureException appExc:
                    var appDet =
                        appExc.Details as OutboundFailureDetails
                        ?? throw new ArgumentException(
                            "Application exception expected to have outbound details");
                    failure.ApplicationFailureInfo = new()
                    {
                        Type = appExc.ErrorType ?? string.Empty,
                        NonRetryable = appExc.NonRetryable,
                        Details =
                            appDet.Count == 0
                                ? null
                                : new() { Payloads_ = { appDet.Details.Select(conv.ToPayload) } },
                        Category = appExc.Category,
                    };
                    if (appExc.NextRetryDelay != null)
                    {
                        failure.ApplicationFailureInfo.NextRetryDelay =
                            Duration.FromTimeSpan((TimeSpan)appExc.NextRetryDelay);
                    }
                    break;
                case CanceledFailureException canExc:
                    var canDet =
                        canExc.Details as OutboundFailureDetails
                        ?? throw new ArgumentException(
                            "Canceled exception expected to have outbound details");
                    failure.CanceledFailureInfo = new()
                    {
                        Details =
                            canDet.Count == 0
                                ? null
                                : new() { Payloads_ = { canDet.Details.Select(conv.ToPayload) } },
                    };
                    break;
                case WorkflowAlreadyStartedException:
                    // We don't need to do anything special for this, but we also don't require it
                    // already have a faiure proto
                    break;
                default:
                    throw new ArgumentException(
                        $"Unexpected failure type {exc.GetType()} without failure proto");
            }
            return failure;
        }

        private Failure CreateFailureFromHandlerException(
            HandlerException exc,
            IPayloadConverter conv)
        {
            // If OriginalFailure is set, this is a round-trip case where an upstream
            // Temporal failure was converted to a NexusRpc Failure and needs to be restored.
            // IMPORTANT: Do NOT set NexusHandlerFailureInfo here — the restored proto already
            // has the correct failure_info type (e.g. ApplicationFailureInfo). Setting
            // NexusHandlerFailureInfo would clear it (protobuf oneof).
            if (exc.OriginalFailure is { } originalFailure)
            {
                return NexusFailureToTemporalFailure(originalFailure, !exc.IsRetryable);
            }

            // Fresh error: create a new Failure proto with NexusHandlerFailureInfo
            return new Failure()
            {
                Message = exc.Message,
                StackTrace = exc.StackTrace ?? string.Empty,
                Cause = exc.InnerException == null ? null : ToFailure(exc.InnerException, conv),
                NexusHandlerFailureInfo = new()
                {
                    Type = exc.RawErrorType,
                    RetryBehavior = exc.ErrorRetryBehavior switch
                    {
                        HandlerErrorRetryBehavior.Retryable => NexusHandlerErrorRetryBehavior.Retryable,
                        HandlerErrorRetryBehavior.NonRetryable => NexusHandlerErrorRetryBehavior.NonRetryable,
                        _ => NexusHandlerErrorRetryBehavior.Unspecified,
                    },
                },
            };
        }

        /// <summary>
        /// Failure converter with
        /// <see cref="DefaultFailureConverterOptions.EncodeCommonAttributes" /> as true.
        /// </summary>
        public class WithEncodedCommonAttributes : DefaultFailureConverter
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="WithEncodedCommonAttributes"/> class.
            /// </summary>
            public WithEncodedCommonAttributes()
                : base(new() { EncodeCommonAttributes = true })
            {
            }
        }
    }
}
