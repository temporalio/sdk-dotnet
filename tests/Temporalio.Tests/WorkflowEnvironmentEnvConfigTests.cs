namespace Temporalio.Tests;

using Temporalio.Common.EnvConfig;
using Xunit;

[Collection("Environment configuration")]
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

    [Fact]
    public async Task InitializeAsync_EnvConfigServerEnabled_UsesConfiguredClientOptions()
    {
        const string GateVariable = "TEMPORAL_TEST_ENV_CONFIG_SERVER";
        const string Namespace = "envconfig-namespace";
        await using var sourceEnv = await Temporalio.Testing.WorkflowEnvironment.StartLocalAsync(
            new() { Namespace = Namespace });
        var configPath = Path.Combine(
            Path.GetTempPath(),
            $"temporalio-dotnet-envconfig-{Guid.NewGuid()}.toml");
        var envVars = new Dictionary<string, string?>
        {
            [GateVariable] = "true",
            ["TEMPORAL_CONFIG_FILE"] = configPath,
            ["TEMPORAL_ADDRESS"] = sourceEnv.Client.Connection.Options.TargetHost,
            ["TEMPORAL_NAMESPACE"] = Namespace,
            ["TEMPORAL_API_KEY"] = "envconfig-api-key",
            ["TEMPORAL_TLS"] = "false",
            ["TEMPORAL_GRPC_META_TEST_HEADER"] = "envconfig-test",
        };
        var originalEnvVars = envVars.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);

        try
        {
            await File.WriteAllTextAsync(configPath, "[profile.default]\n");

            foreach (var pair in envVars)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }

            await using var env = new WorkflowEnvironment();

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
            foreach (var pair in originalEnvVars)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }

            File.Delete(configPath);
        }
    }
}
