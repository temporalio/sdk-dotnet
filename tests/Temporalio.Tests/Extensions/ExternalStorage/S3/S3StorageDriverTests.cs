namespace Temporalio.Tests.Extensions.ExternalStorage.S3;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using Temporalio.Extensions.ExternalStorage.S3;
using Xunit;

public class S3StorageDriverTests
{
    private static readonly IStorageDriverTargetInfo.Workflow WorkflowTarget =
        new("my-ns", "my-workflow-id", "my-run-id", "MyWorkflow");

    [Fact]
    public async Task RetrieveAsync_ClaimsFromStoreAsync_ReturnsTheOriginalPayloads()
    {
        var driver = new S3StorageDriver(new FakeS3Client(), "my-bucket");
        var payloads = new[] { NewPayload("one"), NewPayload("two") };

        var claims = await driver.StoreAsync(StoreContext(WorkflowTarget), payloads, default);

        Assert.Equal(payloads, await driver.RetrieveAsync(RetrieveContext(), claims, default));
    }

    [Fact]
    public async Task StoreAsync_Claim_HoldsBucketKeyAndSha256Hash()
    {
        var driver = new S3StorageDriver(new FakeS3Client(), "my-bucket");

        var claim = Assert.Single(
            await driver.StoreAsync(StoreContext(WorkflowTarget), new[] { NewPayload("one") }, default));

        var hashValue = HashOf("one");
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["bucket"] = "my-bucket",
                ["key"] = $"v0/ns/my-ns/wt/MyWorkflow/wi/my-workflow-id/ri/my-run-id/d/sha256/{hashValue}",
                ["hash_algorithm"] = "sha256",
                ["hash_value"] = hashValue,
            },
            claim.ClaimData);
    }

    [Fact]
    public async Task StoreAsync_WorkflowTarget_BuildsTheSpecExampleKey()
    {
        var target = new IStorageDriverTargetInfo.Workflow(
            "payments prod", "order+123=abc", "3f1d6c7a-8b2e-4f7a-9d0a-87a6f95e4d31", "ChargeWorkflow");

        Assert.Equal(
            "v0/ns/payments%20prod/wt/ChargeWorkflow/wi/order%2B123%3Dabc/ri/3f1d6c7a-8b2e-4f7a-9d0a-87a6f95e4d31" +
            $"/d/sha256/{HashOf("one")}",
            await StoreKeyAsync(target, "one"));
    }

    [Fact]
    public async Task StoreAsync_ActivityTarget_BuildsTheSpecExampleKey()
    {
        var target = new IStorageDriverTargetInfo.Activity(
            "payments prod", "activity id+42", "9e1d1fd9-2f8a-4c40-93e2-731f31b9268b", "Capture/Charge");

        Assert.Equal(
            "v0/ns/payments%20prod/at/Capture%2FCharge/ai/activity%20id%2B42/ri/9e1d1fd9-2f8a-4c40-93e2-731f31b9268b" +
            $"/d/sha256/{HashOf("one")}",
            await StoreKeyAsync(target, "one"));
    }

    [Fact]
    public async Task StoreAsync_KeySegment_EscapesEveryByteOutsideS3SafeCharacters()
    {
        var target = new IStorageDriverTargetInfo.Workflow("AZaz09!-_.*'()", "~é", "run-1", "MyWorkflow");

        Assert.Equal(
            $"v0/ns/AZaz09!-_.*'()/wt/MyWorkflow/wi/%7E%C3%A9/ri/run-1/d/sha256/{HashOf("one")}",
            await StoreKeyAsync(target, "one"));
    }

    [Fact]
    public async Task StoreAsync_EmptyOrMissingTargetValues_WritesNullSegments()
    {
        var target = new IStorageDriverTargetInfo.Workflow(string.Empty, null, string.Empty, null);

        Assert.Equal(
            $"v0/ns/null/wt/null/wi/null/ri/null/d/sha256/{HashOf("one")}",
            await StoreKeyAsync(target, "one"));
    }

    [Fact]
    public async Task StoreAsync_NoTarget_KeyHasOnlyTheDigest()
    {
        Assert.Equal($"v0/d/sha256/{HashOf("one")}", await StoreKeyAsync(null, "one"));
    }

    [Fact]
    public async Task StoreAsync_SamePayloadTwiceInOneCall_UploadsOnce()
    {
        var client = new FakeS3Client();
        var driver = new S3StorageDriver(client, "my-bucket");

        var claims = await driver.StoreAsync(
            StoreContext(WorkflowTarget), new[] { NewPayload("same"), NewPayload("same") }, default);

        Assert.Equal(1, client.PutCount);
        Assert.Equal(claims.First(), claims.Last());
    }

    [Fact]
    public async Task StoreAsync_ObjectAlreadyInS3_SkipsTheUpload()
    {
        var client = new FakeS3Client();
        var driver = new S3StorageDriver(client, "my-bucket");

        await driver.StoreAsync(StoreContext(WorkflowTarget), new[] { NewPayload("same") }, default);
        await driver.StoreAsync(StoreContext(WorkflowTarget), new[] { NewPayload("same") }, default);

        Assert.Equal(1, client.PutCount);
    }

    [Fact]
    public async Task StoreAsync_BucketSelector_ChoosesTheBucketForEachPayload()
    {
        var client = new FakeS3Client();
        var driver = new S3StorageDriver(
            client, (context, payload) => $"bucket-{payload.Data.ToStringUtf8()}");

        var claims = await driver.StoreAsync(
            StoreContext(WorkflowTarget), new[] { NewPayload("a"), NewPayload("b") }, default);

        Assert.Equal(new[] { "bucket-a", "bucket-b" }, claims.Select(claim => claim.ClaimData["bucket"]));
        Assert.Equal(new[] { "bucket-a", "bucket-b" }, client.Objects.Keys.Select(key => key.Bucket).OrderBy(b => b));
    }

    [Fact]
    public async Task StoreAndRetrieve_EachS3Request_TakesOneLimiterPermit()
    {
        var driver = new S3StorageDriver(new FakeS3Client(), "my-bucket");
        var storeLimiter = new RecordingLimiter<Payload>();
        var retrieveLimiter = new RecordingLimiter<StorageDriverClaim>();

        var claims = await driver.StoreAsync(
            new(WorkflowTarget, storeLimiter),
            new[] { NewPayload("one"), NewPayload("two"), NewPayload("one") },
            default);
        await driver.RetrieveAsync(new(retrieveLimiter), claims, default);

        Assert.Equal(
            new[] { NewPayload("one"), NewPayload("two") },
            storeLimiter.Items.OrderBy(payload => payload.Data.ToStringUtf8()));
        Assert.Equal(claims.Count, retrieveLimiter.Items.Count);
    }

    [Fact]
    public async Task StoreAsync_PayloadOverMaxPayloadSize_ThrowsAndAtTheLimitSucceeds()
    {
        var payload = NewPayload("one");
        var size = payload.CalculateSize();
        var atLimit = new S3StorageDriver(new FakeS3Client(), "b") { MaxPayloadSize = size };
        var belowLimit = new S3StorageDriver(new FakeS3Client(), "b") { MaxPayloadSize = size - 1 };

        await atLimit.StoreAsync(StoreContext(), new[] { payload }, default);
        var err = await Assert.ThrowsAsync<ArgumentException>(
            () => belowLimit.StoreAsync(StoreContext(), new[] { payload }, default));
        Assert.Contains("exceeds the configured max payload size", err.Message);
    }

    [Fact]
    public async Task StoreAsync_S3RequestFails_ThrowsWithBucketKeyAndRegion()
    {
        var driver = new S3StorageDriver(new FailingS3Client(), "my-bucket");

        var err = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.StoreAsync(StoreContext(), new[] { NewPayload("one") }, default));

        Assert.Equal(
            $"S3StorageDriver store failed [bucket=my-bucket, key=v0/d/sha256/{HashOf("one")}, " +
            "client_region=us-west-2]",
            err.Message);
        Assert.Equal("access denied", err.InnerException?.Message);
    }

    [Fact]
    public async Task RetrieveAsync_S3RequestFails_ThrowsWithBucketKeyAndRegion()
    {
        var driver = new S3StorageDriver(new FailingS3Client(), "my-bucket");

        var err = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.RetrieveAsync(RetrieveContext(), new[] { NewClaim() }, default));

        Assert.Equal(
            "S3StorageDriver retrieve failed [bucket=my-bucket, key=my-key, client_region=us-west-2]",
            err.Message);
        Assert.Equal("access denied", err.InnerException?.Message);
    }

    [Fact]
    public async Task StoreAsync_OneUploadFails_CancelsTheOthersAndThrowsTheCause()
    {
        var client = new BlockingS3Client(failingKeySuffix: HashOf("fail"));
        var driver = new S3StorageDriver(client, "my-bucket");

        var err = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.StoreAsync(
                StoreContext(), new[] { NewPayload("one"), NewPayload("fail"), NewPayload("two") }, default));

        Assert.Equal("fail", err.InnerException?.Message);
        Assert.Equal(2, client.CanceledCount);
    }

    [Fact]
    public async Task StoreAsync_CallerCancels_ThrowsOperationCanceledNotAnS3Failure()
    {
        var driver = new S3StorageDriver(new BlockingS3Client(), "my-bucket");
        using var cancellationSource = new CancellationTokenSource();

        var store = driver.StoreAsync(StoreContext(), new[] { NewPayload("one") }, cancellationSource.Token);
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store);
    }

    [Fact]
    public async Task RetrieveAsync_DataDoesNotMatchClaimHash_Throws()
    {
        var client = new FakeS3Client();
        var driver = new S3StorageDriver(client, "my-bucket");
        var claim = Assert.Single(await driver.StoreAsync(StoreContext(), new[] { NewPayload("one") }, default));
        client.Objects[("my-bucket", claim.ClaimData["key"])] = NewPayload("tampered").ToByteArray();

        var err = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.RetrieveAsync(RetrieveContext(), new[] { claim }, default));

        Assert.Contains("S3StorageDriver integrity check failed", err.Message);
    }

    [Fact]
    public async Task RetrieveAsync_IncompleteOrUnsupportedClaim_ThrowsBeforeAnyS3Request()
    {
        var client = new FailingS3Client();
        var driver = new S3StorageDriver(client, "my-bucket");
        var invalidClaims = new[]
        {
            NewClaim(bucket: null),
            NewClaim(key: string.Empty),
            NewClaim(hashAlgorithm: null),
            NewClaim(hashValue: null),
            NewClaim(hashAlgorithm: "md5"),
        };

        foreach (var invalidClaim in invalidClaims)
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => driver.RetrieveAsync(RetrieveContext(), new[] { NewClaim(), invalidClaim }, default));
        }
        Assert.Equal(0, client.RequestCount);
    }

    private static Payload NewPayload(string data) =>
        new() { Metadata = { ["encoding"] = ByteString.CopyFromUtf8("json/plain") }, Data = ByteString.CopyFromUtf8(data) };

    private static string HashOf(string data) =>
        Convert.ToHexStringLower(SHA256.HashData(NewPayload(data).ToByteArray()));

    private static StorageDriverStoreContext StoreContext(IStorageDriverTargetInfo? target = null) =>
        new(target, new RecordingLimiter<Payload>());

    private static StorageDriverRetrieveContext RetrieveContext() =>
        new(new RecordingLimiter<StorageDriverClaim>());

    private static async Task<string> StoreKeyAsync(IStorageDriverTargetInfo? target, string data)
    {
        var driver = new S3StorageDriver(new FakeS3Client(), "my-bucket");
        var claims = await driver.StoreAsync(StoreContext(target), new[] { NewPayload(data) }, default);
        return Assert.Single(claims).ClaimData["key"];
    }

    private static StorageDriverClaim NewClaim(
        string? bucket = "my-bucket",
        string? key = "my-key",
        string? hashAlgorithm = "sha256",
        string? hashValue = "abc")
    {
        var claimData = new Dictionary<string, string?>
        {
            ["bucket"] = bucket,
            ["key"] = key,
            ["hash_algorithm"] = hashAlgorithm,
            ["hash_value"] = hashValue,
        };
        return new(claimData.Where(pair => pair.Value != null).ToDictionary(pair => pair.Key, pair => pair.Value!));
    }

    private sealed class RecordingLimiter<TItem> : IStorageDriverLimiter<TItem>
    {
        public ConcurrentQueue<TItem> Items { get; } = new();

        public Task<TResult> RunAsync<TResult>(
            TItem item, Func<Task<TResult>> operation, CancellationToken cancellationToken)
        {
            Items.Enqueue(item);
            return operation();
        }
    }

    private sealed class FakeS3Client : IS3StorageDriverClient
    {
        private int putCount;

        public ConcurrentDictionary<(string Bucket, string Key), byte[]> Objects { get; } = new();

        public int PutCount => putCount;

        public Task PutObjectAsync(string bucket, string key, byte[] data, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref putCount);
            Objects[(bucket, key)] = data;
            return Task.CompletedTask;
        }

        public Task<bool> ObjectExistsAsync(string bucket, string key, CancellationToken cancellationToken) =>
            Task.FromResult(Objects.ContainsKey((bucket, key)));

        public Task<byte[]> GetObjectAsync(string bucket, string key, CancellationToken cancellationToken) =>
            Task.FromResult(Objects[(bucket, key)]);

        public IReadOnlyDictionary<string, string> Describe() => new Dictionary<string, string>();
    }

    private sealed class FailingS3Client : IS3StorageDriverClient
    {
        private int requestCount;

        public int RequestCount => requestCount;

        public Task PutObjectAsync(string bucket, string key, byte[] data, CancellationToken cancellationToken) =>
            Fail<bool>();

        public Task<bool> ObjectExistsAsync(string bucket, string key, CancellationToken cancellationToken) =>
            Fail<bool>();

        public Task<byte[]> GetObjectAsync(string bucket, string key, CancellationToken cancellationToken) =>
            Fail<byte[]>();

        public IReadOnlyDictionary<string, string> Describe() =>
            new Dictionary<string, string> { ["client_region"] = "us-west-2" };

        private Task<T> Fail<T>()
        {
            Interlocked.Increment(ref requestCount);
            return Task.FromException<T>(new InvalidOperationException("access denied"));
        }
    }

    /// <summary>
    /// Blocks every existence check until it is canceled, except the one for a key ending in
    /// failingKeySuffix, which fails once two others are in flight.
    /// </summary>
    private sealed class BlockingS3Client : IS3StorageDriverClient
    {
        private readonly string? failingKeySuffix;
        private readonly TaskCompletionSource otherRequestsStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int startedCount;
        private int canceledCount;

        public BlockingS3Client(string? failingKeySuffix = null) => this.failingKeySuffix = failingKeySuffix;

        public int CanceledCount => canceledCount;

        public Task PutObjectAsync(string bucket, string key, byte[] data, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public async Task<bool> ObjectExistsAsync(string bucket, string key, CancellationToken cancellationToken)
        {
            if (failingKeySuffix != null && key.EndsWith(failingKeySuffix, StringComparison.Ordinal))
            {
                // Fail only once the other requests are in flight, so the test proves they are canceled
                await otherRequestsStarted.Task;
                throw new InvalidOperationException("fail");
            }
            if (Interlocked.Increment(ref startedCount) == 2)
            {
                otherRequestsStarted.SetResult();
            }
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return false;
            }
            catch (OperationCanceledException)
            {
                Interlocked.Increment(ref canceledCount);
                throw;
            }
        }

        public Task<byte[]> GetObjectAsync(string bucket, string key, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public IReadOnlyDictionary<string, string> Describe() => new Dictionary<string, string>();
    }
}
