using Temporalio.Client;
using Xunit;
using Xunit.Abstractions;

namespace Temporalio.Tests.Client;

public class TemporalCloudOperationsClientTests : TestBase
{
    public TemporalCloudOperationsClientTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [SkippableFact]
    public async Task ConnectAsync_SimpleCall_Succeeds()
    {
        var client = await TemporalCloudOperationsClient.ConnectAsync(
            new(Environment.GetEnvironmentVariable("TEMPORAL_CLIENT_CLOUD_API_KEY") ??
                throw new SkipException("No cloud API key"))
            {
                Version = Environment.GetEnvironmentVariable("TEMPORAL_CLIENT_CLOUD_API_VERSION"),
            });
        var ns = Environment.GetEnvironmentVariable("TEMPORAL_CLIENT_CLOUD_NAMESPACE")!;
        var res = await client.Connection.CloudService.GetNamespaceAsync(new() { Namespace = ns });
        Assert.Equal(ns, res.Namespace.Namespace_);
    }
}