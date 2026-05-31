using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Internal helper for building and parsing activity-execution operation tokens used by the
    /// generic <see cref="TemporalOperationHandler"/> when an operation is backed by a standalone
    /// activity.
    /// </summary>
    internal static class NexusActivityExecutionToken
    {
        /// <summary>
        /// Token-type value identifying an activity-execution operation token.
        /// </summary>
        internal const int OperationTokenType = 2;

        /// <summary>
        /// Build a base64url-encoded activity-execution operation token.
        /// </summary>
        /// <param name="namespace_">Activity namespace.</param>
        /// <param name="activityId">Activity ID.</param>
        /// <param name="runId">Activity run ID. May be <c>null</c> when building the token used in
        /// the completion-callback header (which is sent before the run ID is known).</param>
        /// <returns>Base64url-encoded operation token.</returns>
        internal static string Create(string namespace_, string activityId, string? runId) =>
            NexusWorkflowRunHandle.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(
                new Token(namespace_, activityId, runId, null),
                NexusWorkflowRunHandle.TokenSerializerOptions));

        /// <summary>
        /// Parse an activity-execution operation token into its underlying fields.
        /// </summary>
        /// <param name="token">Base64url-encoded token string.</param>
        /// <returns>Parsed token fields.</returns>
        /// <exception cref="ArgumentException">If the token is invalid.</exception>
        internal static Token Parse(string token)
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
            Token? tokenObj;
            try
            {
                tokenObj = JsonSerializer.Deserialize<Token>(
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
            if (tokenObj.Type != OperationTokenType)
            {
                throw new ArgumentException(
                    $"Invalid activity execution token type: {tokenObj.Type}, " +
                    $"expected: {OperationTokenType}");
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
        /// Represents the fields of an activity-execution operation token.
        /// </summary>
        internal record Token(
            [property: JsonPropertyName("ns")]
            string Namespace,
            [property: JsonPropertyName("aid")]
            string ActivityId,
            [property: JsonPropertyName("rid")]
            string? RunId,
            [property: JsonPropertyName("v")]
            int? Version,
            [property: JsonPropertyName("t")]
            int Type = OperationTokenType);
    }
}
