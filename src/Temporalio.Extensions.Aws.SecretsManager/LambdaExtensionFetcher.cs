using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Temporalio.Extensions.Aws.SecretsManager
{
    /// <summary>
    /// Fetches secrets through the AWS Parameters and Secrets Lambda Extension. See
    /// https://docs.aws.amazon.com/secretsmanager/latest/userguide/retrieving-secrets_lambda.html.
    /// </summary>
    internal sealed class LambdaExtensionFetcher : IFetcher
    {
        private const string TokenHeader = "X-Aws-Parameters-Secrets-Token";

        // No proxy, so that proxy settings never apply to requests to the local extension
#pragma warning disable CA5399 // Plain HTTP to localhost, so there are no certificates to check
        private static readonly HttpClient Client = new(new HttpClientHandler { UseProxy = false })
        {
            // Each request has its own timeout, so that it can be told apart from other failures
            Timeout = Timeout.InfiniteTimeSpan,
        };
#pragma warning restore CA5399

        private readonly string endpoint;
        private readonly Func<string, string?> getEnv;

        /// <summary>
        /// Initializes a new instance of the <see cref="LambdaExtensionFetcher"/> class.
        /// </summary>
        /// <param name="port">Lambda extension port.</param>
        /// <param name="getEnv">Reads process environment variables.</param>
        public LambdaExtensionFetcher(string port, Func<string, string?> getEnv)
        {
            endpoint = $"http://{Fetchers.LambdaExtensionHost}:{port}/secretsmanager/get?secretId=";
            this.getEnv = getEnv;
        }

        /// <inheritdoc />
        public string Path => Telemetry.FetchPathExtension;

        /// <inheritdoc />
        public async Task<SecretValue> FetchAsync(string secretId)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, endpoint + Uri.EscapeDataString(secretId));
            request.Headers.Add(TokenHeader, getEnv("AWS_SESSION_TOKEN") ?? string.Empty);
            using var cts = new CancellationTokenSource(Fetchers.FetchTimeout);
            string body;
            int status;
            try
            {
                using var response = await Client.SendAsync(request, cts.Token).ConfigureAwait(false);
                status = (int)response.StatusCode;
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException e) when (cts.IsCancellationRequested)
            {
                throw new FetchTimeoutException(
                    $"AWS Secrets Manager secret fetch timed out after {Fetchers.FetchTimeout}", e);
            }
            if (status != 200)
            {
                throw new LambdaExtensionStatusException(status, body);
            }
            return ParseResponse(body);
        }

        /// <summary>
        /// Parse a Lambda extension response body.
        /// </summary>
        /// <param name="body">Response body.</param>
        /// <returns>The secret value.</returns>
        internal static SecretValue ParseResponse(string body)
        {
            JsonDocument doc;
            try
            {
                // Same shape as the GetSecretValue response, with SecretBinary base64
                doc = JsonDocument.Parse(body);
            }
            catch (JsonException e)
            {
                throw Invalid(e.Message, e);
            }
            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    throw Invalid("not an object");
                }
                var secretString = StringField(root, "SecretString");
                var secretBinary = StringField(root, "SecretBinary");
                byte[]? binary = null;
                if (!string.IsNullOrEmpty(secretBinary))
                {
                    try
                    {
                        binary = Convert.FromBase64String(secretBinary);
                    }
                    catch (FormatException e)
                    {
                        throw Invalid($"invalid SecretBinary: {e.Message}", e);
                    }
                }
                return SecretValue.FromResponse(secretString, binary, StringField(root, "VersionId"));
            }
        }

        private static string? StringField(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out var field) || field.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            if (field.ValueKind != JsonValueKind.String)
            {
                throw Invalid($"{name} must be a string");
            }
            return field.GetString();
        }

        private static InvalidResponseException Invalid(string message, Exception? inner = null) =>
            new($"failed decoding AWS Parameters and Secrets Lambda Extension response: {message}", inner);
    }
}
