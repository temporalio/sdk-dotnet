using System;
using System.Collections.Generic;
using System.Linq;
using Temporalio.Api.Failure.V1;
using Temporalio.Exceptions;

namespace Temporalio.Converters
{
    /// <summary>
    /// Default implementation of <see cref="IFailureConverter" />.
    /// </summary>
    public class DefaultFailureConverter : IFailureConverter
    {
        /// <summary>
        /// Options this converter was created with.
        /// </summary>
        /// <remarks>
        /// Callers should never mutate this. Rather they should subclass the failure converter and
        /// pass a different value into the protected constructor.
        /// </remarks>
        public DefaultFailureConverterOptions Options { get; private init; }

        /// <summary>
        /// Create a new failure converter.
        /// </summary>
        public DefaultFailureConverter() : this(new()) { }

        /// <summary>
        /// Create a new failure converter with the given options.
        /// </summary>
        /// <param name="options">Options for the failure converter</param>
        /// <remarks>
        /// This is protected because payload converters are referenced as class types, not
        /// instances, so only subclasses would call this.
        /// </remarks>
        protected DefaultFailureConverter(DefaultFailureConverterOptions options)
        {
            Options = options;
        }

        /// <inheritdoc />
        public Failure ToFailure(Exception exception, IPayloadConverter payloadConverter)
        {
            // If the exception is not already a failure exception, make it an application exception
            var failureEx =
                exception as FailureException
                ?? new ApplicationFailureException(
                    exception.Message,
                    exception.InnerException,
                    exception.GetType().Name
                );

            // Create new failure object. This means if it's already set we copy it. This is costly,
            // but we prefer it over potentially mutating the failure on the existing exception. We
            // pass in the stack trace from the original exception always just in case it was
            // converted to an application failure which won't have the original stack trace.
            var failure = CreateFailureFromException(
                failureEx,
                exception.StackTrace,
                payloadConverter
            );

            // If requested, move message and stack trace to encoded attributes
            if (Options.EncodeCommonAttributes)
            {
                failure.EncodedAttributes = payloadConverter.ToPayload(
                    new Dictionary<string, string>
                    {
                        ["message"] = failure.Message,
                        ["stack_trace"] = failure.StackTrace
                    }
                );
                failure.Message = "Encoded failure";
                failure.StackTrace = "";
            }
            return failure;
        }

        private Failure CreateFailureFromException(
            FailureException exc,
            string? stackTrace,
            IPayloadConverter conv
        )
        {
            // Copy existing failure if already there
            if (exc.Failure != null)
            {
                return new(exc.Failure);
            }
            var failure = new Failure()
            {
                Message = exc.Message,
                StackTrace = stackTrace ?? "",
                Cause = exc.InnerException == null ? null : ToFailure(exc.InnerException, conv)
            };
            switch (exc)
            {
                case ApplicationFailureException appExc:
                    var appDet =
                        appExc.Details as OutboundFailureDetails
                        ?? throw new ArgumentException(
                            "Application exception expected to have outbound details"
                        );
                    failure.ApplicationFailureInfo = new()
                    {
                        Type = appExc.Type ?? "",
                        NonRetryable = appExc.NonRetryable,
                        Details =
                            appDet.Count == 0
                                ? null
                                : new() { Payloads_ = { appDet.Details.Select(conv.ToPayload) } }
                    };
                    break;
                case CancelledFailureException canExc:
                    var canDet =
                        canExc.Details as OutboundFailureDetails
                        ?? throw new ArgumentException(
                            "Cancelled exception expected to have outbound details"
                        );
                    failure.CanceledFailureInfo = new()
                    {
                        Details =
                            canDet.Count == 0
                                ? null
                                : new() { Payloads_ = { canDet.Details.Select(conv.ToPayload) } }
                    };
                    break;
                default:
                    throw new ArgumentException(
                        $"Unexpected failure type {exc.GetType()} without failure proto"
                    );
            }
            return failure;
        }

        /// <inheritdoc />
        public Exception ToException(Failure failure, IPayloadConverter payloadConverter)
        {
            // If encoded attributes are present and we can decode the attributes, set them as
            // expected
            if (failure.EncodedAttributes != null)
            {
                try
                {
                    var attrs = payloadConverter.ToValue<Dictionary<string, string>>(
                        failure.EncodedAttributes
                    )!;
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
                    return new CancelledFailureException(failure, inner, payloadConverter);
                case Failure.FailureInfoOneofCase.TerminatedFailureInfo:
                    return new TerminatedFailureException(failure, inner);
                case Failure.FailureInfoOneofCase.ActivityFailureInfo:
                    return new ActivityFailureException(failure, inner);
                case Failure.FailureInfoOneofCase.ChildWorkflowExecutionFailureInfo:
                    return new ChildWorkflowFailureException(failure, inner);
                default:
                    return new FailureException(failure, inner);
            }
        }

        /// <summary>
        /// Failure converter with
        /// <see cref="DefaultFailureConverterOptions.EncodeCommonAttributes" /> as true.
        /// </summary>
        public class WithEncodedCommonAttributes : DefaultFailureConverter
        {
            /// <summary>
            /// Create failure converter with
            /// <see cref="DefaultFailureConverterOptions.EncodeCommonAttributes" /> as true.
            /// </summary>
            public WithEncodedCommonAttributes() : base(new() { EncodeCommonAttributes = true }) { }
        }
    }
}
