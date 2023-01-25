using System;
using Temporalio.Api.Common.V1;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception representing a gRPC failure.
    /// </summary>
    public class RpcException : TemporalException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RpcException"/> class.
        /// </summary>
        /// <param name="code">gRPC status code.</param>
        /// <param name="message">Message.</param>
        /// <param name="rawStatus">gRPC status as protobuf.</param>
        public RpcException(StatusCode code, string message, byte[]? rawStatus)
            : base(message)
        {
            Code = code;
            RawStatus = rawStatus;
            GrpcStatus = new(() =>
            {
                if (rawStatus == null)
                {
                    return null;
                }
                return Api.Common.V1.GrpcStatus.Parser.ParseFrom(rawStatus);
            });
        }

        /// <summary>
        /// gRPC status code taken from
        /// https://github.com/grpc/grpc-dotnet/blob/master/src/Grpc.Core.Api/StatusCode.cs.
        /// </summary>
        public enum StatusCode
        {
            /// <summary>Not an error; returned on success.</summary>
            OK = 0,

            /// <summary>The operation was cancelled (typically by the caller).</summary>
            Cancelled = 1,

            /// <summary>
            /// Unknown error.  An example of where this error may be returned is
            /// if a Status value received from another address space belongs to
            /// an error-space that is not known in this address space.  Also
            /// errors raised by APIs that do not return enough error information
            /// may be converted to this error.
            /// </summary>
            Unknown = 2,

            /// <summary>
            /// Client specified an invalid argument.  Note that this differs
            /// from FAILED_PRECONDITION.  INVALID_ARGUMENT indicates arguments
            /// that are problematic regardless of the state of the system
            /// (e.g., a malformed file name).
            /// </summary>
            InvalidArgument = 3,

            /// <summary>
            /// Deadline expired before operation could complete.  For operations
            /// that change the state of the system, this error may be returned
            /// even if the operation has completed successfully.  For example, a
            /// successful response from a server could have been delayed long
            /// enough for the deadline to expire.
            /// </summary>
            DeadlineExceeded = 4,

            /// <summary>Some requested entity (e.g., file or directory) was not found.</summary>
            NotFound = 5,

            /// <summary>Some entity that we attempted to create (e.g., file or directory) already exists.</summary>
            AlreadyExists = 6,

            /// <summary>
            /// The caller does not have permission to execute the specified
            /// operation.  PERMISSION_DENIED must not be used for rejections
            /// caused by exhausting some resource (use RESOURCE_EXHAUSTED
            /// instead for those errors).  PERMISSION_DENIED must not be
            /// used if the caller can not be identified (use UNAUTHENTICATED
            /// instead for those errors).
            /// </summary>
            PermissionDenied = 7,

            /// <summary>The request does not have valid authentication credentials for the operation.</summary>
            Unauthenticated = 16,

            /// <summary>
            /// Some resource has been exhausted, perhaps a per-user quota, or
            /// perhaps the entire file system is out of space.
            /// </summary>
            ResourceExhausted = 8,

            /// <summary>
            /// Operation was rejected because the system is not in a state
            /// required for the operation's execution.  For example, directory
            /// to be deleted may be non-empty, an rmdir operation is applied to
            /// a non-directory, etc.
            /// </summary>
            FailedPrecondition = 9,

            /// <summary>
            /// The operation was aborted, typically due to a concurrency issue
            /// like sequencer check failures, transaction aborts, etc.
            /// </summary>
            Aborted = 10,

            /// <summary>
            /// Operation was attempted past the valid range.  E.g., seeking or
            /// reading past end of file.
            /// </summary>
            OutOfRange = 11,

            /// <summary>Operation is not implemented or not supported/enabled in this service.</summary>
            Unimplemented = 12,

            /// <summary>
            /// Internal errors.  Means some invariants expected by underlying
            /// system has been broken.  If you see one of these errors,
            /// something is very broken.
            /// </summary>
            Internal = 13,

            /// <summary>
            /// The service is currently unavailable.  This is a most likely a
            /// transient condition and may be corrected by retrying with
            /// a backoff. Note that it is not always safe to retry
            /// non-idempotent operations.
            /// </summary>
            Unavailable = 14,

            /// <summary>Unrecoverable data loss or corruption.</summary>
            DataLoss = 15,
        }

        /// <summary>
        /// Gets the gRPC status code of the error.
        /// </summary>
        public StatusCode Code { get; private init; }

        /// <summary>
        /// Gets the gRPC status message as a protobuf.
        /// </summary>
        public byte[]? RawStatus { get; private init; }

        /// <summary>
        /// Gets a lazily-created gRPC status from the details.
        /// </summary>
        public Lazy<GrpcStatus?> GrpcStatus { get; private init; }
    }
}