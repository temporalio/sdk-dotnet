namespace Temporalio.Tests.Extensions.ExternalStorage.S3;

using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Temporalio.Extensions.ExternalStorage.S3.AwsSdk;
using Xunit;

public class AwsSdkS3StorageDriverClientTests
{
    [Fact]
    public async Task ObjectExistsAsync_Returns200AsTrueAnd404AsFalse()
    {
        var handler = new StubHandler(_ => new(HttpStatusCode.OK));
        using var amazonClient = NewAmazonS3Client(handler);
        var client = new AwsSdkS3StorageDriverClient(amazonClient);
        Assert.True(await client.ObjectExistsAsync("my-bucket", "my-key", default));

        handler.Respond = _ => new(HttpStatusCode.NotFound);
        Assert.False(await client.ObjectExistsAsync("my-bucket", "my-key", default));
    }

    [Fact]
    public async Task ObjectExistsAsync_ErrorOtherThan404_Throws()
    {
        var handler = new StubHandler(_ => new(HttpStatusCode.Forbidden));
        using var amazonClient = NewAmazonS3Client(handler);
        var client = new AwsSdkS3StorageDriverClient(amazonClient);

        var err = await Assert.ThrowsAsync<AmazonS3Exception>(
            () => client.ObjectExistsAsync("my-bucket", "my-key", default));
        Assert.Equal(HttpStatusCode.Forbidden, err.StatusCode);
    }

    [Fact]
    public async Task PutObjectAsync_SendsDataToBucketAndKey()
    {
        var handler = new StubHandler(_ => new(HttpStatusCode.OK));
        using var amazonClient = NewAmazonS3Client(handler);
        var client = new AwsSdkS3StorageDriverClient(amazonClient);

        await client.PutObjectAsync("my-bucket", "my-key", new byte[] { 0, 1 }, default);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/my-bucket/my-key", request.Path);
        Assert.Equal(new byte[] { 0, 1 }, request.Body);
    }

    [Fact]
    public async Task GetObjectAsync_ReturnsTheResponseBody()
    {
        var handler = new StubHandler(_ => new(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 0, 1 }),
        });
        using var amazonClient = NewAmazonS3Client(handler);
        var client = new AwsSdkS3StorageDriverClient(amazonClient);

        Assert.Equal(new byte[] { 0, 1 }, await client.GetObjectAsync("my-bucket", "my-key", default));
    }

    [Fact]
    public void Describe_ReturnsTheClientRegion()
    {
        using var amazonClient = new AmazonS3Client(new AnonymousAWSCredentials(), RegionEndpoint.USWest2);
        var client = new AwsSdkS3StorageDriverClient(amazonClient);

        Assert.Equal(new Dictionary<string, string> { ["client_region"] = "us-west-2" }, client.Describe());
    }

    private static AmazonS3Client NewAmazonS3Client(StubHandler handler) =>
        new(
            new AnonymousAWSCredentials(),
            new AmazonS3Config
            {
                ServiceURL = "http://localhost",
                ForcePathStyle = true,
                HttpClientFactory = new StubHttpClientFactory(handler),
            });

    private sealed record RecordedRequest(HttpMethod Method, string Path, byte[] Body);

    private sealed class StubHandler : HttpMessageHandler
    {
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => Respond = respond;

        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; }

        public List<RecordedRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ?
                Array.Empty<byte>() : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            Requests.Add(new(request.Method, request.RequestUri!.AbsolutePath, body));
            return Respond(request);
        }
    }

    private sealed class StubHttpClientFactory : HttpClientFactory
    {
        private readonly StubHandler handler;

        public StubHttpClientFactory(StubHandler handler) => this.handler = handler;

        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => new(handler, false);
    }
}
