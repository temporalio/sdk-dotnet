using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Temporalio.Extensions.ExternalStorage.S3
{
    /// <summary>
    /// Storage driver that stores and retrieves payloads in Amazon S3.
    /// </summary>
    /// <remarks>
    /// Payloads are content addressed by a SHA-256 hash of their serialized bytes, so storing the
    /// same payload twice for the same execution uploads it once. Object keys are built from the
    /// namespace and the workflow or activity the payload is stored for, percent-encoded to S3's
    /// safe character set.
    /// </remarks>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    internal sealed class S3StorageDriver : IStorageDriver
    {
        /// <summary>
        /// Driver type shared by every S3 driver instance across SDK languages.
        /// </summary>
        public const string DriverType = "aws.s3driver";

        /// <summary>
        /// Default maximum serialized payload size, in bytes.
        /// </summary>
        public const int DefaultMaxPayloadSize = 50 * 1024 * 1024;

        private const string ClaimBucket = "bucket";
        private const string ClaimKey = "key";
        private const string ClaimHashAlgorithm = "hash_algorithm";
        private const string ClaimHashValue = "hash_value";
        private const string NullSegment = "null";
        private const string SafeSpecialCharacters = "!-_.*'()";

        private readonly IS3StorageDriverClient client;
        private readonly Func<StorageDriverStoreContext, Payload, string> bucketSelector;
        private readonly int maxPayloadSize = DefaultMaxPayloadSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="S3StorageDriver" /> class that stores every
        /// payload in one bucket.
        /// </summary>
        /// <param name="client">Client that performs the S3 requests.</param>
        /// <param name="bucket">Bucket name.</param>
        public S3StorageDriver(IS3StorageDriverClient client, string bucket)
            : this(client, (context, payload) => bucket)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="S3StorageDriver" /> class that chooses the
        /// bucket for each payload.
        /// </summary>
        /// <param name="client">Client that performs the S3 requests.</param>
        /// <param name="bucketSelector">Returns the bucket name for a payload.</param>
        public S3StorageDriver(
            IS3StorageDriverClient client,
            Func<StorageDriverStoreContext, Payload, string> bucketSelector)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.bucketSelector =
                bucketSelector ?? throw new ArgumentNullException(nameof(bucketSelector));
        }

        /// <summary>
        /// Gets the driver instance name written into history. Defaults to
        /// <see cref="DriverType" />. Only change this when registering multiple S3 drivers with
        /// different configurations on the same external storage.
        /// </summary>
        public string Name { get; init; } = DriverType;

        /// <summary>
        /// Gets the driver type. Always <see cref="DriverType" />.
        /// </summary>
        public string Type => DriverType;

        /// <summary>
        /// Gets the maximum serialized payload size, in bytes. Storing a larger payload throws.
        /// Defaults to <see cref="DefaultMaxPayloadSize" />.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">If set to zero or less.</exception>
        public int MaxPayloadSize
        {
            get => maxPayloadSize;
            init
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(MaxPayloadSize), value, "Max payload size must be positive.");
                }
                maxPayloadSize = value;
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyCollection<StorageDriverClaim>> StoreAsync(
            StorageDriverStoreContext context,
            IReadOnlyCollection<Payload> payloads,
            CancellationToken cancellationToken)
        {
            var contextSegments = ContextKeySegments(context.Target);
            var objects = payloads
                .Select(payload => NewStoredObject(context, payload, contextSegments))
                .ToList();
            var uniqueObjects = objects
                .GroupBy(obj => (obj.Bucket, obj.Key))
                .Select(group => group.First());
            await RunAllAsync(
                uniqueObjects,
                (obj, token) => context.Limiter.RunAsync(
                    obj.Payload, () => UploadIfAbsentAsync(obj, token), token),
                cancellationToken).ConfigureAwait(false);
            return objects.Select(NewClaim).ToList();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyCollection<Payload>> RetrieveAsync(
            StorageDriverRetrieveContext context,
            IReadOnlyCollection<StorageDriverClaim> claims,
            CancellationToken cancellationToken)
        {
            foreach (var claim in claims)
            {
                ValidateClaim(claim);
            }
            return await RunAllAsync(
                claims,
                (claim, token) => context.Limiter.RunAsync(
                    claim, () => DownloadAsync(claim, token), token),
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Runs the operation for every item concurrently. The first failure cancels the rest, and
        /// that failure is thrown once they have all finished.
        /// </summary>
        private static async Task<TResult[]> RunAllAsync<TItem, TResult>(
            IEnumerable<TItem> items,
            Func<TItem, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken)
        {
            using var cancellationSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Exception? firstError = null;
            var tasks = items.Select(async item =>
            {
                try
                {
                    return await operation(item, cancellationSource.Token).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Interlocked.CompareExchange(ref firstError, e, null);
                    cancellationSource.Cancel();
                    throw;
                }
            }).ToList();
            try
            {
                return await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch when (firstError != null)
            {
                // Task.WhenAll throws the first failure in list order, which may be a sibling's
                // cancellation rather than the failure that caused it
                ExceptionDispatchInfo.Capture(firstError).Throw();
                throw;
            }
        }

        private static void ValidateClaim(StorageDriverClaim claim)
        {
            var bucket = ClaimValue(claim, ClaimBucket);
            var key = ClaimValue(claim, ClaimKey);
            if (bucket == null || key == null)
            {
                throw new ArgumentException(
                    "S3StorageDriver claim is missing required location information: claim data " +
                    "must contain 'bucket' and 'key'");
            }
            var hashAlgorithm = ClaimValue(claim, ClaimHashAlgorithm);
            if (hashAlgorithm == null || ClaimValue(claim, ClaimHashValue) == null)
            {
                throw new ArgumentException(
                    "S3StorageDriver claim is missing required content hash information " +
                    $"[bucket={bucket}, key={key}]: claim data must contain 'hash_algorithm' and " +
                    "'hash_value'");
            }
            if (hashAlgorithm != "sha256")
            {
                throw new ArgumentException(
                    $"S3StorageDriver unsupported hash algorithm [bucket={bucket}, key={key}]: " +
                    $"expected sha256, got {hashAlgorithm}");
            }
        }

        private static StorageDriverClaim NewClaim(StoredObject obj) =>
            new(new Dictionary<string, string>
            {
                [ClaimBucket] = obj.Bucket,
                [ClaimKey] = obj.Key,
                [ClaimHashAlgorithm] = "sha256",
                [ClaimHashValue] = obj.HashValue,
            });

        private static string? ClaimValue(StorageDriverClaim claim, string name) =>
            claim.ClaimData.TryGetValue(name, out var value) && value.Length > 0 ? value : null;

        private static string Sha256Hex(byte[] data)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(data);
            var hex = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
            {
                hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }
            return hex.ToString();
        }

        private static string ContextKeySegments(IStorageDriverTargetInfo? target) =>
            target switch
            {
                IStorageDriverTargetInfo.Workflow workflow =>
                    $"/ns/{EncodeKeySegment(workflow.Namespace)}" +
                    $"/wt/{EncodeKeySegment(workflow.WorkflowType)}" +
                    $"/wi/{EncodeKeySegment(workflow.WorkflowId)}" +
                    $"/ri/{EncodeKeySegment(workflow.RunId)}",
                IStorageDriverTargetInfo.Activity activity =>
                    $"/ns/{EncodeKeySegment(activity.Namespace)}" +
                    $"/at/{EncodeKeySegment(activity.ActivityType)}" +
                    $"/ai/{EncodeKeySegment(activity.ActivityId)}" +
                    $"/ri/{EncodeKeySegment(activity.RunId)}",
                _ => string.Empty,
            };

        /// <summary>
        /// Escapes every UTF-8 byte outside S3's safe character set, so keys are portable across S3
        /// implementations and identical to the keys other SDK languages build.
        /// </summary>
        private static string EncodeKeySegment(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return NullSegment;
            }
            var encoded = new StringBuilder();
            foreach (var b in Encoding.UTF8.GetBytes(value))
            {
                var c = (char)b;
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ||
                    SafeSpecialCharacters.IndexOf(c) >= 0)
                {
                    encoded.Append(c);
                }
                else
                {
                    encoded.Append('%').Append(
                        b.ToString("X2", CultureInfo.InvariantCulture));
                }
            }
            return encoded.ToString();
        }

        private StoredObject NewStoredObject(
            StorageDriverStoreContext context, Payload payload, string contextSegments)
        {
            var bucket = bucketSelector(context, payload);
            var data = payload.ToByteArray();
            if (data.Length > MaxPayloadSize)
            {
                throw new ArgumentException(
                    $"Payload size {data.Length} bytes exceeds the configured max payload size " +
                    $"of {MaxPayloadSize} bytes");
            }
            var hashValue = Sha256Hex(data);
            return new(payload, bucket, $"v0{contextSegments}/d/sha256/{hashValue}", hashValue, data);
        }

        /// <summary>
        /// Upload the object unless it already exists.
        /// </summary>
        /// <returns>Whether the object was uploaded.</returns>
        private async Task<bool> UploadIfAbsentAsync(
            StoredObject obj, CancellationToken cancellationToken)
        {
            try
            {
                if (await client.ObjectExistsAsync(
                    obj.Bucket, obj.Key, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }
                await client.PutObjectAsync(
                    obj.Bucket, obj.Key, obj.Data, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"S3StorageDriver store failed [bucket={obj.Bucket}, key={obj.Key}{ClientContext()}]",
                    e);
            }
        }

        private async Task<Payload> DownloadAsync(
            StorageDriverClaim claim, CancellationToken cancellationToken)
        {
            var bucket = claim.ClaimData[ClaimBucket];
            var key = claim.ClaimData[ClaimKey];
            byte[] data;
            try
            {
                data = await client.GetObjectAsync(bucket, key, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"S3StorageDriver retrieve failed [bucket={bucket}, key={key}{ClientContext()}]", e);
            }
            var expectedHash = claim.ClaimData[ClaimHashValue];
            var actualHash = Sha256Hex(data);
            if (actualHash != expectedHash)
            {
                throw new InvalidOperationException(
                    $"S3StorageDriver integrity check failed [bucket={bucket}, key={key}]: " +
                    $"expected sha256:{expectedHash}, got sha256:{actualHash}");
            }
            return Payload.Parser.ParseFrom(data);
        }

        private string ClientContext() =>
            string.Concat(client.Describe().Select(pair => $", {pair.Key}={pair.Value}"));

        private sealed record StoredObject(
            Payload Payload, string Bucket, string Key, string HashValue, byte[] Data);
    }
}
