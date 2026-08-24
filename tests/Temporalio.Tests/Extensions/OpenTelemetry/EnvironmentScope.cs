namespace Temporalio.Tests.Extensions.OpenTelemetry;

internal sealed class EnvironmentScope : IDisposable
{
    private readonly IReadOnlyDictionary<string, string?> previousValues;

    public EnvironmentScope(params KeyValuePair<string, string?>[] values)
    {
        previousValues = values.ToDictionary(
            pair => pair.Key,
            pair => Environment.GetEnvironmentVariable(pair.Key));
        foreach (var pair in values)
        {
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }

    public void Dispose()
    {
        foreach (var pair in previousValues)
        {
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }
}
