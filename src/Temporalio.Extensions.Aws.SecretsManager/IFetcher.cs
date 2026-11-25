using System.Threading.Tasks;

namespace Temporalio.Extensions.Aws.SecretsManager
{
    /// <summary>
    /// Fetches secret values by ID. Must be safe to call concurrently.
    /// </summary>
    internal interface IFetcher
    {
        /// <summary>
        /// Gets the fetch_path metric label for this fetcher.
        /// </summary>
        string Path { get; }

        /// <summary>
        /// Fetch the current value of the given secret.
        /// </summary>
        /// <param name="secretId">Secret ID.</param>
        /// <returns>The secret value.</returns>
        Task<SecretValue> FetchAsync(string secretId);
    }
}
