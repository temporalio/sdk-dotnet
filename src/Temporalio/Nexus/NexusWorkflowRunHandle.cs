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
        private static readonly JsonSerializerOptions TokenSerializerOptions = new()
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
        /// Create a handle based on the string token.
        /// </summary>
        /// <param name="token">Operation token.</param>
        /// <returns>Created handle.</returns>
        /// <exception cref="ArgumentException">If the token is invalid.</exception>
        internal static NexusWorkflowRunHandle FromToken(string token)
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(token);
            }
            catch (FormatException)
            {
                throw new ArgumentException("Token invalid");
            }
            var tokenObj = JsonSerializer.Deserialize<Token>(bytes, TokenSerializerOptions) ??
                throw new ArgumentException("Token invalid");
            if (tokenObj.Version != null && tokenObj.Version != 0)
            {
                throw new ArgumentException($"Unsupported token version: {tokenObj.Version}");
            }
            return new(tokenObj.Namespace, tokenObj.WorkflowId, tokenObj.Version ?? 0);
        }

        /// <summary>
        /// Create a string token based on this handle.
        /// </summary>
        /// <returns>Operation token.</returns>
        internal string ToToken() => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(
            new Token(Namespace, WorkflowId, Version == 0 ? null : Version),
            TokenSerializerOptions));

        private record Token(
            [property: JsonPropertyName("ns")]
            string Namespace,
            [property: JsonPropertyName("wid")]
            string WorkflowId,
            [property: JsonPropertyName("v")]
            int? Version,
            [property: JsonPropertyName("t'")]
            int Type = 1);
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