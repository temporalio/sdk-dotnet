using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Temporalio.Extensions.ExternalStorage.S3
{
    /// <summary>
    /// S3 object operations used by <see cref="S3StorageDriver" />. Implement this to use an S3
    /// library other than the AWS SDK.
    /// </summary>
    /// <remarks>
    /// Implementations are called concurrently, so they must be thread-safe.
    /// </remarks>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    internal interface IS3StorageDriverClient
    {
        /// <summary>
        /// Upload data to the given bucket and key.
        /// </summary>
        /// <param name="bucket">Bucket name.</param>
        /// <param name="key">Object key.</param>
        /// <param name="data">Data to upload.</param>
        /// <param name="cancellationToken">Cancellation token for the request.</param>
        /// <returns>Task for completion.</returns>
        Task PutObjectAsync(
            string bucket, string key, byte[] data, CancellationToken cancellationToken);

        /// <summary>
        /// Check whether an object exists at the given bucket and key.
        /// </summary>
        /// <param name="bucket">Bucket name.</param>
        /// <param name="key">Object key.</param>
        /// <param name="cancellationToken">Cancellation token for the request.</param>
        /// <returns>Whether the object exists.</returns>
        Task<bool> ObjectExistsAsync(
            string bucket, string key, CancellationToken cancellationToken);

        /// <summary>
        /// Download the object at the given bucket and key.
        /// </summary>
        /// <param name="bucket">Bucket name.</param>
        /// <param name="key">Object key.</param>
        /// <param name="cancellationToken">Cancellation token for the request.</param>
        /// <returns>Object data.</returns>
        Task<byte[]> GetObjectAsync(
            string bucket, string key, CancellationToken cancellationToken);

        /// <summary>
        /// Gets client diagnostic information, such as the region, that the driver adds to error
        /// messages.
        /// </summary>
        /// <returns>Diagnostic information, or an empty dictionary.</returns>
        IReadOnlyDictionary<string, string> Describe();
    }
}
