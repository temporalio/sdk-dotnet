namespace Temporalio.Tests;

using Temporalio.Common.EnvConfig;
using Xunit;

[Collection("Environment configuration")]
public class WorkflowEnvironmentEnvConfigTests
{
    [Fact]
    public async Task InitializeAsync_EnvConfigServerEnabled_UsesConfiguredClientOptions()
    {
        const string GateVariable = "TEMPORAL_TEST_ENV_CONFIG_SERVER";
        const string Namespace = "envconfig-namespace";
        var originalGate = Environment.GetEnvironmentVariable(GateVariable);
        await using var sourceEnv = await Temporalio.Testing.WorkflowEnvironment.StartLocalAsync(
            new() { Namespace = Namespace });
        await using var env = new WorkflowEnvironment(
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

        try
        {
            Environment.SetEnvironmentVariable(GateVariable, "true");
            await env.InitializeAsync();

            var connectionOptions = env.Client.Connection.Options;
            Assert.Equal(sourceEnv.Client.Connection.Options.TargetHost, connectionOptions.TargetHost);
            Assert.Equal(Namespace, env.Client.Options.Namespace);
            Assert.Equal("envconfig-api-key", connectionOptions.ApiKey);
            Assert.Equal(
                "envconfig-test",
                connectionOptions.RpcMetadata!.Single(pair => pair.Key == "test-header").Value);
            Assert.True(connectionOptions.Tls!.Disabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable(GateVariable, originalGate);
        }
    }
}
