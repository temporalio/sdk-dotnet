#pragma warning disable SA1402 // We allow multiple types of the same name

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Run handle that is expected to be returned in functions passed to <c>FromHandleFactory</c>
    /// on <see cref="WorkflowRunOperationHandler"/>. It is returned from <c>StartWorkflowAsync</c>
    /// calls on <see cref="WorkflowRunOperationContext"/>.
    /// </summary>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public class NexusWorkflowRunHandle
    {
        /// <summary>
        /// Token-type value identifying a workflow-run operation token.
        /// </summary>
        internal const int WorkflowRunOperationTokenType = 1;

        /// <summary>
        /// Token-type value identifying an update-workflow operation token.
        /// </summary>
        internal const int UpdateWorkflowOperationTokenType = 3;

        /// <summary>
        /// Serializer options shared by all operation token types.
        /// </summary>
        internal static readonly JsonSerializerOptions TokenSerializerOptions = new()
        {
#pragma warning disable SYSLIB0020 // Need to use obsolete form, alternative not in all our versions
            IgnoreNullValues = true,
#pragma warning restore SYSLIB0020
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="NexusWorkflowRunHandle"/> class.
        /// </summary>
        /// <param name="namespace_">Workflow namespace.</param>
        /// <param name="workflowId">Workflow ID.</param>
        /// <param name="version">Operation token version.</param>
        internal NexusWorkflowRunHandle(
            string namespace_,
            string workflowId,
            int version)
        {
            Namespace = namespace_;
            WorkflowId = workflowId;
            Version = version;
        }

        /// <summary>
        /// Gets the namespace.
        /// </summary>
        internal string Namespace { get; private init; }

        /// <summary>
        /// Gets the workflow ID.
        /// </summary>
        internal string WorkflowId { get; private init; }

        /// <summary>
        /// Gets the token version.
        /// </summary>
        internal int Version { get; private init; }

        /// <summary>
        /// Encode bytes to a base64url string with no padding.
        /// </summary>
        /// <param name="data">Bytes to encode.</param>
        /// <returns>Base64url encoded string.</returns>
        internal static string Base64UrlEncode(byte[] data) =>
            Convert.ToBase64String(data)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');

        /// <summary>
        /// Decode a base64url string to bytes.
        /// </summary>
        /// <param name="s">Base64url encoded string.</param>
        /// <returns>Decoded bytes.</returns>
        internal static byte[] Base64UrlDecode(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            return Convert.FromBase64String(s);
        }

        /// <summary>
        /// Create a handle based on the string token.
        /// </summary>
        /// <param name="token">Operation token.</param>
        /// <returns>Created handle.</returns>
        /// <exception cref="ArgumentException">If the token is invalid.</exception>
        internal static NexusWorkflowRunHandle FromToken(string token)
        {
            var data = ParseToken(token);
            return new(data.Namespace, data.WorkflowId, data.Version ?? 0);
        }

        /// <summary>
        /// Parse an operation token to its underlying fields. Validates encoding, JSON shape, and
        /// version (but not type — callers decide which token types they support).
        /// </summary>
        /// <param name="token">Base64url-encoded token string.</param>
        /// <returns>Parsed token fields.</returns>
        /// <exception cref="ArgumentException">If the token is invalid.</exception>
        internal static OperationToken ParseToken(string token)
        {
            byte[] bytes;
            try
            {
                bytes = Base64UrlDecode(token);
            }
            catch (FormatException)
            {
                throw new ArgumentException("Token invalid");
            }
            OperationToken? tokenObj;
            try
            {
                tokenObj = JsonSerializer.Deserialize<OperationToken>(bytes, TokenSerializerOptions);
            }
            catch (JsonException e)
            {
                throw new ArgumentException("Token invalid", e);
            }
            if (tokenObj == null)
            {
                throw new ArgumentException("Token invalid");
            }
            if (tokenObj.Version != null && tokenObj.Version != 0)
            {
                throw new ArgumentException($"Unsupported token version: {tokenObj.Version}");
            }
            return tokenObj;
        }

        /// <summary>
        /// Create a string token based on this handle.
        /// </summary>
        /// <returns>Operation token.</returns>
        internal string ToToken() => Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(
            new OperationToken(Namespace, WorkflowId, Version == 0 ? null : Version),
            TokenSerializerOptions));

        /// <summary>
        /// Represents the fields of a Nexus operation token. The <c>RunId</c> and <c>UpdateId</c>
        /// fields are only populated for update-workflow tokens; for workflow-run tokens they are
        /// null and omitted from the serialized form.
        /// </summary>
        internal record OperationToken(
            [property: JsonPropertyName("ns")]
            string Namespace,
            [property: JsonPropertyName("wid")]
            string WorkflowId,
            [property: JsonPropertyName("v")]
            int? Version,
            [property: JsonPropertyName("t")]
            int Type = WorkflowRunOperationTokenType,
            [property: JsonPropertyName("rid")]
            string? RunId = null,
            [property: JsonPropertyName("uid")]
            string? UpdateId = null);
    }

    /// <inheritdoc />
    public class NexusWorkflowRunHandle<TResult> : NexusWorkflowRunHandle
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NexusWorkflowRunHandle{TResult}"/> class.
        /// </summary>
        /// <param name="namespace_">Workflow namespace.</param>
        /// <param name="workflowId">Workflow ID.</param>
        /// <param name="version">Operation token version.</param>
        internal NexusWorkflowRunHandle(
            string namespace_,
            string workflowId,
            int version)
            : base(namespace_, workflowId, version)
        {
        }
    }
}