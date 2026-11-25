namespace Temporalio.Tests.Extensions.Aws.SecretsManager;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Temporalio.Extensions.Aws.SecretsManager;

/// <summary>
/// Telemetry that records metric updates.
/// </summary>
internal sealed class RecordingTelemetry : Telemetry
{
    private readonly List<(string Name, Dictionary<string, object> Tags)> updates = new();

    public RecordingTelemetry(ILogger? logger = null)
        : base(null, logger ?? NullLogger.Instance)
    {
    }

    public override void RecordCacheLookup(IEnumerable<KeyValuePair<string, object>> tags) =>
        Add(MetricCacheLookup, tags);

    public override void RecordFetchFailed(IEnumerable<KeyValuePair<string, object>> tags) =>
        Add(MetricFetchFailed, tags);

    public override void RecordFetchLatency(
        TimeSpan latency, IEnumerable<KeyValuePair<string, object>> tags) =>
        Add(MetricFetchLatency, tags);

    /// <summary>
    /// Returns and clears the tags of each update of the given metric.
    /// </summary>
    /// <param name="name">Metric name.</param>
    /// <returns>The tags of each update.</returns>
    public List<Dictionary<string, object>> Take(string name)
    {
        lock (updates)
        {
            var taken = updates.Where(u => u.Name == name).Select(u => u.Tags).ToList();
            updates.RemoveAll(u => u.Name == name);
            return taken;
        }
    }

    private void Add(string name, IEnumerable<KeyValuePair<string, object>> tags)
    {
        lock (updates)
        {
            updates.Add((name, tags.ToDictionary(kv => kv.Key, kv => kv.Value)));
        }
    }
}
