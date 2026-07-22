namespace Temporalio.Tests;

using Xunit;

[Collection("Environment configuration")]
public class WorkflowEnvironmentEnvConfigTests
{
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
            File.WriteAllText(configPath, "[profile.default]\n");

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
