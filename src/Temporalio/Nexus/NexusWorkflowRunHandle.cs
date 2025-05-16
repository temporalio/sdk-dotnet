using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Temporalio.Nexus
{
    public class NexusWorkflowRunHandle<TResult>
    {
        private static readonly JsonSerializerOptions TokenSerializerOptions = new()
        {
            IgnoreNullValues = true,
        };

        internal NexusWorkflowRunHandle(
            string namespace_,
            string workflowId,
            int version = 0)
        {
            Namespace = namespace_;
            WorkflowId = workflowId;
            Version = version;
        }

        internal string Namespace { get; private init; }

        internal string WorkflowId { get; private init; }

        internal int Version { get; private init; }

        internal string ToToken() => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(
            new Token(Namespace, WorkflowId, Version == 0 ? null : Version),
            TokenSerializerOptions));

        internal static NexusWorkflowRunHandle<TResult> FromToken(string token)
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
}