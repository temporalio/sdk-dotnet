namespace Temporalio.Tests.Extensions.Aws.SecretsManager;

using System.Collections.Concurrent;
using Temporalio.Extensions.Aws.SecretsManager;

/// <summary>
/// Returns configured values, counting fetches. A value may be an exception to throw. If
/// <see cref="Gate" /> is set, fetches wait for it.
/// </summary>
internal sealed class FakeFetcher : IFetcher
{
    private readonly ConcurrentDictionary<string, object> values = new();
    private readonly ConcurrentDictionary<string, int> calls = new();

    public FakeFetcher(string path = Telemetry.FetchPathSdk) => Path = path;

    public string Path { get; }

    public TaskCompletionSource? Gate { get; set; }

    public void Set(string secretId, string value, string versionId) =>
        Set(secretId, System.Text.Encoding.UTF8.GetBytes(value), versionId);

    public void Set(string secretId, byte[] value, string versionId) =>
        values[secretId] = new SecretValue(value, versionId);

    public void Fail(string secretId, Exception err) => values[secretId] = err;

    public int Calls(string secretId) => calls.TryGetValue(secretId, out var n) ? n : 0;

    public async Task<SecretValue> FetchAsync(string secretId)
    {
        calls.AddOrUpdate(secretId, 1, (_, n) => n + 1);
        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        if (!values.TryGetValue(secretId, out var value))
        {
            throw new InvalidOperationException($"no fake value for {secretId}");
        }
        if (value is Exception e)
        {
            throw e;
        }
        return (SecretValue)value;
    }
}
