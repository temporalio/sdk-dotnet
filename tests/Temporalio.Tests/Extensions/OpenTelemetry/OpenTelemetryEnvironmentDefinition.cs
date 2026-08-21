namespace Temporalio.Tests.Extensions.OpenTelemetry;

using Xunit;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OpenTelemetryEnvironmentDefinition
{
    public const string Name = "OpenTelemetryEnvironment";
}
