using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;

namespace Temporalio.Extensions.ExternalStorage.S3.AwsSdk
{
    /// <summary>
    /// <see cref="IS3StorageDriverClient" /> backed by an <see cref="IAmazonS3" /> from the
    /// official AWS SDK.
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    internal sealed class AwsSdkS3StorageDriverClient : IS3StorageDriverClient
    {
        private readonly IAmazonS3 client;

        /// <summary>
        /// Initializes a new instance of the <see cref="AwsSdkS3StorageDriverClient" /> class.
        /// </summary>
        /// <param name="client">AWS S3 client to make requests with.</param>
        public AwsSdkS3StorageDriverClient(IAmazonS3 client) =>
            this.client = client ?? throw new ArgumentNullException(nameof(client));

        /// <inheritdoc />
        public async Task PutObjectAsync(
            string bucket, string key, byte[] data, CancellationToken cancellationToken)
        {
            using var stream = new MemoryStream(data);
            await client.PutObjectAsync(
                new() { BucketName = bucket, Key = key, InputStream = stream },
                cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ObjectExistsAsync(
            string bucket, string key, CancellationToken cancellationToken)
        {
            try
            {
                await client.GetObjectMetadataAsync(bucket, key, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<byte[]> GetObjectAsync(
            string bucket, string key, CancellationToken cancellationToken)
        {
            using var response = await client.GetObjectAsync(bucket, key, cancellationToken).ConfigureAwait(false);
            using var data = new MemoryStream();
            await response.ResponseStream.CopyToAsync(data, 81920, cancellationToken).ConfigureAwait(false);
            return data.ToArray();
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string> Describe()
        {
            var info = new Dictionary<string, string>();
            if (client.Config.RegionEndpoint?.SystemName is { Length: > 0 } region)
            {
                info["client_region"] = region;
            }
            return info;
        }
    }
}
