using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace Temporalio.Extensions.Aws.SecretsManager
{
    /// <summary>
    /// Fetches secrets directly from Secrets Manager. If no client is given, one is created from
    /// the default AWS config on first use.
    /// </summary>
    internal sealed class SdkFetcher : IFetcher
    {
        private readonly Lazy<IAmazonSecretsManager> client;

        /// <summary>
        /// Initializes a new instance of the <see cref="SdkFetcher"/> class.
        /// </summary>
        /// <param name="client">Client, or null to create one from the default AWS config.</param>
        public SdkFetcher(IAmazonSecretsManager? client) =>
            this.client = client != null ?
                new(() => client) :
                new(() => new AmazonSecretsManagerClient(), LazyThreadSafetyMode.ExecutionAndPublication);

        /// <inheritdoc />
        public string Path => Telemetry.FetchPathSdk;

        /// <inheritdoc />
        public async Task<SecretValue> FetchAsync(string secretId)
        {
            using var cts = new CancellationTokenSource(Fetchers.FetchTimeout);
            GetSecretValueResponse resp;
            try
            {
                resp = await client.Value.GetSecretValueAsync(
                    new() { SecretId = secretId }, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException e) when (cts.IsCancellationRequested)
            {
                throw new FetchTimeoutException(
                    $"AWS Secrets Manager secret fetch timed out after {Fetchers.FetchTimeout}", e);
            }
            return SecretValue.FromResponse(
                resp.SecretString, resp.SecretBinary?.ToArray(), resp.VersionId);
        }
    }
}
