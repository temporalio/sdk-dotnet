#pragma warning disable SA1402 // We allow multiple types of the same name

using System;
using System.Text.Json;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Update handle representing an update-workflow operation started from a Nexus operation. It
    /// carries the identifiers needed to encode/decode an update-workflow operation token.
    /// </summary>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    internal class NexusWorkflowUpdateHandle
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NexusWorkflowUpdateHandle"/> class.
        /// </summary>
        /// <param name="namespace_">Workflow namespace.</param>
        /// <param name="workflowId">Workflow ID.</param>
        /// <param name="runId">Workflow run ID. May be empty.</param>
        /// <param name="updateId">Update ID.</param>
        /// <param name="version">Operation token version.</param>
        public NexusWorkflowUpdateHandle(
            string namespace_,
            string workflowId,
            string runId,
            string updateId,
            int version)
        {
            Namespace = namespace_;
            WorkflowId = workflowId;
            RunId = runId;
            UpdateId = updateId;
            Version = version;
        }

        /// <summary>
        /// Gets the namespace.
        /// </summary>
        public string Namespace { get; private init; }

        /// <summary>
        /// Gets the workflow ID.
        /// </summary>
        public string WorkflowId { get; private init; }

        /// <summary>
        /// Gets the workflow run ID. May be empty.
        /// </summary>
        public string RunId { get; private init; }

        /// <summary>
        /// Gets the update ID.
        /// </summary>
        public string UpdateId { get; private init; }

        /// <summary>
        /// Gets the token version.
        /// </summary>
        public int Version { get; private init; }

        /// <summary>
        /// Create a handle based on the string token.
        /// </summary>
        /// <param name="token">Operation token.</param>
        /// <returns>Created handle.</returns>
        /// <exception cref="ArgumentException">If the token is invalid.</exception>
        public static NexusWorkflowUpdateHandle FromToken(string token)
        {
            var data = NexusWorkflowRunHandle.ParseToken(token);
            if (string.IsNullOrEmpty(data.Namespace))
            {
                throw new ArgumentException("Invalid token: missing namespace");
            }
            if (data.Type != NexusWorkflowRunHandle.UpdateWorkflowOperationTokenType)
            {
                throw new ArgumentException(
                    $"Invalid token type: {data.Type}, expected: " +
                    $"{NexusWorkflowRunHandle.UpdateWorkflowOperationTokenType}");
            }
            if (string.IsNullOrEmpty(data.WorkflowId))
            {
                throw new ArgumentException("Invalid token: missing workflow ID (wid)");
            }
            if (string.IsNullOrEmpty(data.UpdateId))
            {
                throw new ArgumentException("Invalid token: missing update ID (uid)");
            }
            return new(
                data.Namespace,
                data.WorkflowId,
                data.RunId ?? string.Empty,
                data.UpdateId!,
                data.Version ?? 0);
        }

        /// <summary>
        /// Create a string token based on this handle.
        /// </summary>
        /// <returns>Operation token.</returns>
        /// <exception cref="ArgumentException">If the namespace, workflow ID, or update ID is
        /// empty. An empty update ID would produce a token that breaks server-side dedup, so it is
        /// rejected here (matching the Go and TypeScript token generators).</exception>
        public string ToToken()
        {
            if (string.IsNullOrEmpty(Namespace) ||
                string.IsNullOrEmpty(WorkflowId) ||
                string.IsNullOrEmpty(UpdateId))
            {
                throw new ArgumentException(
                    "Cannot create update-workflow operation token: namespace, workflow ID, and " +
                    $"update ID must all be non-empty (ns: '{Namespace}', wid: '{WorkflowId}', " +
                    $"uid: '{UpdateId}')");
            }
            return NexusWorkflowRunHandle.Base64UrlEncode(
                JsonSerializer.SerializeToUtf8Bytes(
                    new NexusWorkflowRunHandle.OperationToken(
                        Namespace: Namespace,
                        WorkflowId: WorkflowId,
                        Version: Version == 0 ? null : Version,
                        Type: NexusWorkflowRunHandle.UpdateWorkflowOperationTokenType,
                        RunId: string.IsNullOrEmpty(RunId) ? null : RunId,
                        UpdateId: UpdateId),
                    NexusWorkflowRunHandle.TokenSerializerOptions));
        }
    }
}
