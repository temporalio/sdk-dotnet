namespace Temporalio.Tests;

using Temporalio.Common.EnvConfig;
using Xunit;

public class WorkflowEnvironmentEnvConfigTests
{
    [Fact]
    public async Task CreateFromEnvConfigAsync_ProfileLoadOptions_UsesConfiguredClientOptions()
    {
        const string Namespace = "envconfig-options-namespace";
        await using var sourceEnv = await Temporalio.Testing.WorkflowEnvironment.StartLocalAsync(
            new() { Namespace = Namespace });

        await using var env = await Temporalio.Testing.WorkflowEnvironment.CreateFromEnvConfigAsync(
            new()
            {
                ConfigSource = DataSource.FromUTF8String(
                    $$"""
                    [profile.default]
                    address = "{{sourceEnv.Client.Connection.Options.TargetHost}}"
                    namespace = "{{Namespace}}"
                    api_key = "envconfig-api-key"

                    [profile.default.tls]
                    disabled = true

                    [profile.default.grpc_meta]
                    test-header = "envconfig-test"
                    """),
                DisableEnv = true,
            });

        var connectionOptions = env.Client.Connection.Options;
        Assert.Equal(sourceEnv.Client.Connection.Options.TargetHost, connectionOptions.TargetHost);
        Assert.Equal(Namespace, env.Client.Options.Namespace);
        Assert.Equal("envconfig-api-key", connectionOptions.ApiKey);
        Assert.Equal(
            "envconfig-test",
            connectionOptions.RpcMetadata!.Single(pair => pair.Key == "test-header").Value);
        Assert.True(connectionOptions.Tls!.Disabled);
    }
}
