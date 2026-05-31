#pragma warning disable SA1402 // We allow multiple types of the same name

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Run handle for a standalone activity execution backing a Nexus operation.
    /// Returned from <c>StartActivityAsync</c> calls on <see cref="ITemporalNexusClient"/>.
    /// </summary>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public class NexusActivityExecutionHandle
    {
        /// <summary>
        /// Token-type value identifying an activity-execution operation token.
        /// </summary>
        internal const int ActivityExecutionOperationTokenType = 4;

        /// <summary>
        /// Initializes a new instance of the <see cref="NexusActivityExecutionHandle"/> class.
        /// </summary>
        /// <param name="namespace_">Activity namespace.</param>
        /// <param name="activityId">Activity ID.</param>
        /// <param name="version">Operation token version.</param>
        internal NexusActivityExecutionHandle(
            string namespace_,
            string activityId,
            int version)
        {
            Namespace = namespace_;
            ActivityId = activityId;
            Version = version;
        }

        /// <summary>
        /// Gets the namespace.
        /// </summary>
        internal string Namespace { get; private init; }

        /// <summary>
        /// Gets the activity ID.
        /// </summary>
        internal string ActivityId { get; private init; }

        /// <summary>
        /// Gets the token version.
        /// </summary>
        internal int Version { get; private init; }

        /// <summary>
        /// Create a handle based on the string token.
        /// </summary>
        /// <param name="token">Operation token.</param>
        /// <returns>Created handle.</returns>
        /// <exception cref="ArgumentException">If the token is invalid.</exception>
        internal static NexusActivityExecutionHandle FromToken(string token)
        {
            var data = ParseToken(token);
            return new(data.Namespace, data.ActivityId, data.Version ?? 0);
        }

        /// <summary>
        /// Parse an activity-execution operation token to its underlying fields.
        /// </summary>
        /// <param name="token">Base64url-encoded token string.</param>
        /// <returns>Parsed token fields.</returns>
        /// <exception cref="ArgumentException">If the token is invalid.</exception>
        internal static OperationToken ParseToken(string token)
        {
            byte[] bytes;
            try
            {
                bytes = NexusWorkflowRunHandle.Base64UrlDecode(token);
            }
            catch (FormatException)
            {
                throw new ArgumentException("Token invalid");
            }
            OperationToken? tokenObj;
            try
            {
                tokenObj = JsonSerializer.Deserialize<OperationToken>(
                    bytes, NexusWorkflowRunHandle.TokenSerializerOptions);
            }
            catch (JsonException e)
            {
                throw new ArgumentException("Token invalid", e);
            }
            if (tokenObj == null)
            {
                throw new ArgumentException("Token invalid");
            }
            if (tokenObj.Type != ActivityExecutionOperationTokenType)
            {
                throw new ArgumentException(
                    $"Invalid activity execution token type: {tokenObj.Type}, " +
                    $"expected: {ActivityExecutionOperationTokenType}");
            }
            if (tokenObj.Version != null && tokenObj.Version != 0)
            {
                throw new ArgumentException($"Unsupported token version: {tokenObj.Version}");
            }
            if (string.IsNullOrEmpty(tokenObj.ActivityId))
            {
                throw new ArgumentException("Token invalid: missing activity ID (aid)");
            }
            return tokenObj;
        }

        /// <summary>
        /// Create a string token based on this handle.
        /// </summary>
        /// <returns>Operation token.</returns>
        internal string ToToken() => NexusWorkflowRunHandle.Base64UrlEncode(
            JsonSerializer.SerializeToUtf8Bytes(
                new OperationToken(Namespace, ActivityId, Version == 0 ? null : Version),
                NexusWorkflowRunHandle.TokenSerializerOptions));

        /// <summary>
        /// Represents the fields of a Nexus activity-execution operation token.
        /// </summary>
        internal record OperationToken(
            [property: JsonPropertyName("ns")]
            string Namespace,
            [property: JsonPropertyName("aid")]
            string ActivityId,
            [property: JsonPropertyName("v")]
            int? Version,
            [property: JsonPropertyName("t")]
            int Type = ActivityExecutionOperationTokenType);
    }

    /// <inheritdoc />
    public class NexusActivityExecutionHandle<TResult> : NexusActivityExecutionHandle
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NexusActivityExecutionHandle{TResult}"/> class.
        /// </summary>
        /// <param name="namespace_">Activity namespace.</param>
        /// <param name="activityId">Activity ID.</param>
        /// <param name="version">Operation token version.</param>
        internal NexusActivityExecutionHandle(
            string namespace_,
            string activityId,
            int version)
            : base(namespace_, activityId, version)
        {
        }
    }
}
