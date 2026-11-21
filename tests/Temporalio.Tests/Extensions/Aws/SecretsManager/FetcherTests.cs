namespace Temporalio.Tests.Extensions.Aws.SecretsManager;

using System.Net;
using System.Net.Sockets;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Temporalio.Extensions.Aws.SecretsManager;
using Xunit;

public class FetcherTests
{
    // Version IDs must be at least 32 characters
    private const string Version1 = "00000000-0000-0000-0000-000000000001";
    private const string Version2 = "00000000-0000-0000-0000-000000000002";

    [Fact]
    public async Task LambdaExtensionFetcher_FetchesSecrets()
    {
        using var server = new FakeLambdaExtension(id => id == "binary/secret" ?
            (200, $"{{\"SecretBinary\": \"{Convert.ToBase64String(new byte[] { 1, 2, 3 })}\", \"VersionId\": \"v2\"}}") :
            (200, "{\"SecretString\": \"value\", \"VersionId\": \"v1\"}"));
        var env = new Dictionary<string, string?> { ["AWS_SESSION_TOKEN"] = "token" };
        var fetcher = new LambdaExtensionFetcher(server.Port.ToString(), name => env.GetValueOrDefault(name));
        Assert.Equal("extension", fetcher.Path);
        var value = await fetcher.FetchAsync("string");
        Assert.Equal("value", System.Text.Encoding.UTF8.GetString(value.Value));
        Assert.Equal("v1", value.VersionId);
        value = await fetcher.FetchAsync("binary/secret");
        Assert.Equal(new byte[] { 1, 2, 3 }, value.Value);
        Assert.Equal("v2", value.VersionId);
        Assert.Equal(
            new[]
            {
                "/secretsmanager/get?secretId=string token=token",
                "/secretsmanager/get?secretId=binary%2Fsecret token=token",
            },
            server.Requests);
    }

    [Theory]
    [InlineData(400, "bad", "client_error")]
    [InlineData(500, "oops", "server_error")]
    [InlineData(200, "not json", "invalid_response")]
    [InlineData(200, "\"string\"", "invalid_response")]
    [InlineData(200, "{\"SecretString\": 1}", "invalid_response")]
    [InlineData(200, "{\"VersionId\": \"v1\"}", "invalid_response")]
    public async Task LambdaExtensionFetcher_Errors(int status, string body, string reason)
    {
        using var server = new FakeLambdaExtension(_ => (status, body));
        var fetcher = new LambdaExtensionFetcher(server.Port.ToString(), _ => null);
        var e = await Assert.ThrowsAnyAsync<Exception>(() => fetcher.FetchAsync("id"));
        Assert.Equal(reason, Telemetry.ClassifyFailure(e));
    }

    [Fact]
    public async Task LambdaExtensionFetcher_ConnectionRefused()
    {
        var fetcher = new LambdaExtensionFetcher(ClosedPort().ToString(), _ => null);
        var e = await Assert.ThrowsAnyAsync<Exception>(() => fetcher.FetchAsync("id"));
        Assert.Equal("other", Telemetry.ClassifyFailure(e));
    }

    [Fact]
    public async Task SdkFetcher_FetchesSecrets()
    {
        using var client = new FakeSecretsManagerClient(req => req.SecretId == "binary" ?
            new() { SecretBinary = new MemoryStream(new byte[] { 1, 2 }), VersionId = Version2 } :
            new() { SecretString = "value", VersionId = Version1 });
        var fetcher = new SdkFetcher(client);
        Assert.Equal("sdk", fetcher.Path);
        Assert.Equal("value", System.Text.Encoding.UTF8.GetString((await fetcher.FetchAsync("string")).Value));
        Assert.Equal(new byte[] { 1, 2 }, (await fetcher.FetchAsync("binary")).Value);
    }

    [Fact]
    public async Task SdkFetcher_Errors()
    {
        using var notFoundClient = new FakeSecretsManagerClient(_ =>
            throw new ResourceNotFoundException("not found") { ErrorCode = "ResourceNotFoundException" });
        var fetcher = new SdkFetcher(notFoundClient);
        Assert.Equal(
            "resource_not_found",
            Telemetry.ClassifyFailure(await Assert.ThrowsAnyAsync<Exception>(() => fetcher.FetchAsync("id"))));

        using var emptyClient = new FakeSecretsManagerClient(_ => new() { VersionId = Version1 });
        fetcher = new SdkFetcher(emptyClient);
        var e = await Assert.ThrowsAnyAsync<Exception>(() => fetcher.FetchAsync("id"));
        Assert.Equal("invalid_response", Telemetry.ClassifyFailure(e));
        Assert.Equal("secret has no value", e.Message);
    }

    [Theory]
    [InlineData("ResourceNotFoundException", "resource_not_found")]
    [InlineData("AccessDeniedException", "access_denied")]
    [InlineData("DecryptionFailure", "decryption_failure")]
    [InlineData("InvalidRequestException", "invalid_request")]
    [InlineData("InvalidParameterException", "invalid_request")]
    [InlineData("ThrottlingException", "throttling")]
    [InlineData("InternalServiceError", "internal_service_error")]
    [InlineData("SomethingElse", "other")]
    public void ClassifyFailure_AwsErrors(string code, string reason) =>
        Assert.Equal(
            reason,
            Telemetry.ClassifyFailure(new AmazonSecretsManagerException("err") { ErrorCode = code }));

    [Fact]
    public void ClassifyFailure_OtherErrors()
    {
        Assert.Equal("invalid_response", Telemetry.ClassifyFailure(new InvalidResponseException("bad")));
        Assert.Equal("timeout", Telemetry.ClassifyFailure(new FetchTimeoutException("slow")));
        Assert.Equal("client_error", Telemetry.ClassifyFailure(new LambdaExtensionStatusException(403, string.Empty)));
        Assert.Equal("server_error", Telemetry.ClassifyFailure(new LambdaExtensionStatusException(503, string.Empty)));
        Assert.Equal("other", Telemetry.ClassifyFailure(new LambdaExtensionStatusException(302, string.Empty)));
        Assert.Equal("other", Telemetry.ClassifyFailure(new InvalidOperationException()));
    }

    [Fact]
    public void NewDefaultFetcher_SelectsFetcher()
    {
        var env = new Dictionary<string, string?> { [Fetchers.LambdaExtensionPortEnvVar] = "1234" };
        Assert.IsType<LambdaExtensionFetcher>(Fetchers.NewDefaultFetcher(name => env.GetValueOrDefault(name)));

        // Empty disables detection, even in Lambda
        env[Fetchers.LambdaExtensionPortEnvVar] = string.Empty;
        env["AWS_LAMBDA_FUNCTION_NAME"] = "fn";
        Assert.IsType<SdkFetcher>(Fetchers.NewDefaultFetcher(name => env.GetValueOrDefault(name)));

        // Not probed outside Lambda
        Assert.IsType<SdkFetcher>(Fetchers.NewDefaultFetcher(_ => null));
    }

    [Fact]
    public void Probe_DetectsListener()
    {
        using var server = new FakeLambdaExtension(_ => (200, string.Empty));
        Assert.True(Fetchers.Probe(server.Port));
        Assert.False(Fetchers.Probe(ClosedPort()));
    }

    private static int ClosedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// A fake AWS Parameters and Secrets Lambda Extension that responds with the given status and
    /// body, recording requests.
    /// </summary>
    private sealed class FakeLambdaExtension : IDisposable
    {
        private readonly HttpListener listener;
        private readonly Func<string, (int Status, string Body)> respond;
        private readonly Task loop;

        public FakeLambdaExtension(Func<string, (int Status, string Body)> respond)
        {
            this.respond = respond;
            // HttpListener can't bind port 0, so get a free port from a TcpListener, which can.
            // Something else may take it before HttpListener binds it, so retry with another if
            // that happens.
            for (var attempt = 1; ; attempt++)
            {
                Port = ClosedPort();
                listener = new();
                listener.Prefixes.Add($"http://localhost:{Port}/");
                try
                {
                    listener.Start();
                    break;
                }
                catch (HttpListenerException) when (attempt < 5)
                {
                    listener.Close();
                }
            }
            loop = Task.Run(LoopAsync);
        }

        public int Port { get; }

        public List<string> Requests { get; } = new();

        public void Dispose()
        {
            listener.Stop();
            listener.Close();
        }

        private async Task LoopAsync()
        {
            while (listener.IsListening)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await listener.GetContextAsync();
                }
                catch (HttpListenerException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                var id = ctx.Request.QueryString["secretId"] ?? string.Empty;
                lock (Requests)
                {
                    Requests.Add(
                        $"{ctx.Request.Url!.AbsolutePath}?{ctx.Request.Url.Query.TrimStart('?')} " +
                        $"token={ctx.Request.Headers["X-Aws-Parameters-Secrets-Token"]}");
                }
                var (status, body) = respond(id);
                var bytes = System.Text.Encoding.UTF8.GetBytes(body);
                ctx.Response.StatusCode = status;
                await ctx.Response.OutputStream.WriteAsync(bytes);
                ctx.Response.Close();
            }
        }
    }
}
