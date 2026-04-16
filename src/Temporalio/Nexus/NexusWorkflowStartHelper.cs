using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NexusRpc.Handlers;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Internal helper for starting workflows from Nexus operations and managing operation tokens.
    /// Shared by both <see cref="WorkflowRunOperationContext"/> and <see cref="TemporalNexusClient"/>.
    /// </summary>
    internal static class NexusWorkflowStartHelper
    {
        private static readonly JsonSerializerOptions TokenSerializerOptions = new()
        {
#pragma warning disable SYSLIB0020 // Need to use obsolete form, alternative not in all our versions
            IgnoreNullValues = true,
#pragma warning restore SYSLIB0020
        };

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
        /// Generate a base64url-encoded operation token for a workflow run.
        /// </summary>
        /// <param name="namespace_">Workflow namespace.</param>
        /// <param name="workflowId">Workflow ID.</param>
        /// <param name="tokenType">Token type (1 = workflow run).</param>
        /// <param name="version">Token version.</param>
        /// <returns>Base64url-encoded token string.</returns>
        internal static string GenerateToken(
            string namespace_, string workflowId, int tokenType = 1, int? version = null) =>
            Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(
                new OperationToken(namespace_, workflowId, version, tokenType),
                TokenSerializerOptions));

        /// <summary>
        /// Parse an operation token to extract its fields.
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
        /// Start a workflow and return the operation token. This handles all Nexus plumbing:
        /// cloning options, setting task queue, processing links, injecting callbacks, and
        /// adding outbound links.
        /// </summary>
        /// <param name="client">Temporal client.</param>
        /// <param name="nexusStartContext">Nexus start context for callbacks and links.</param>
        /// <param name="temporalContext">Temporal operation context for info and logging.</param>
        /// <param name="workflow">Workflow type name.</param>
        /// <param name="args">Workflow arguments.</param>
        /// <param name="options">Workflow start options. ID and TaskQueue are required.</param>
        /// <returns>Base64url-encoded operation token.</returns>
        internal static async Task<string> StartWorkflowAndGetTokenAsync(
            ITemporalClient client,
            OperationStartContext nexusStartContext,
            NexusOperationExecutionContext temporalContext,
            string workflow,
            IReadOnlyCollection<object?> args,
            WorkflowOptions options)
        {
            var namespace_ = client.Options.Namespace;
            var workflowId = options.Id ?? string.Empty;

            // Generate the token before starting the workflow (needed for callback header)
            var token = GenerateToken(namespace_, workflowId);

            // Shallow clone the options so we can mutate them. We just overwrite any of these
            // internal options since they cannot be user set at this time.
            options = (WorkflowOptions)options.Clone();
            options.TaskQueue ??= temporalContext.Info.TaskQueue;
            if (options.IdConflictPolicy == WorkflowIdConflictPolicy.UseExisting)
            {
                options.OnConflictOptions = new()
                {
                    AttachLinks = true,
                    AttachCompletionCallbacks = true,
                    AttachRequestId = true,
                };
            }
            if (nexusStartContext.InboundLinks.Count > 0)
            {
                options.Links = nexusStartContext.InboundLinks.Select(link =>
                {
                    try
                    {
                        return new Link { WorkflowEvent = link.ToWorkflowEvent() };
                    }
                    catch (ArgumentException e)
                    {
                        temporalContext.Logger.LogWarning(e, "Invalid Nexus link: {Url}", link.Uri);
                        return null;
                    }
                }).OfType<Link>().ToList();
            }
            if (nexusStartContext.CallbackUrl is { } callbackUrl)
            {
                var callback = new Callback() { Nexus = new() { Url = callbackUrl } };
                if (nexusStartContext.CallbackHeaders is { } callbackHeaders)
                {
                    foreach (var kv in callbackHeaders)
                    {
                        callback.Nexus.Header.Add(kv.Key, kv.Value);
                    }
                }
                // Set operation token
                if (nexusStartContext.CallbackHeaders?.ContainsKey("Nexus-Operation-Token") != true)
                {
                    callback.Nexus.Header["Nexus-Operation-Token"] = token;
                }
                if (options.Links is { } links)
                {
                    callback.Links.AddRange(links);
                }
                options.CompletionCallbacks = new[] { callback };
            }
            options.RequestId = nexusStartContext.RequestId;

            // Do the start call
            var wfHandle = await client.StartWorkflowAsync(
                workflow, args, options).ConfigureAwait(false);

            // Add the outbound link
            nexusStartContext.OutboundLinks.Add(new Link.Types.WorkflowEvent
            {
                Namespace = namespace_,
                WorkflowId = workflowId,
                RunId = wfHandle.FirstExecutionRunId ??
                    throw new InvalidOperationException("Handle unexpectedly missing run ID"),
                EventRef = new() { EventId = 1, EventType = EventType.WorkflowExecutionStarted },
            }.ToNexusLink());

            return token;
        }

        /// <summary>
        /// Represents the fields of a Nexus operation token.
        /// </summary>
        internal record OperationToken(
            [property: JsonPropertyName("ns")]
            string Namespace,
            [property: JsonPropertyName("wid")]
            string WorkflowId,
            [property: JsonPropertyName("v")]
            int? Version,
            [property: JsonPropertyName("t")]
            int Type = 1);
    }
}
