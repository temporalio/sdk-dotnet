using System.Collections.Generic;

namespace Temporalio.Runtime
{
    public interface ICustomMetricMeter
    {
        // TODO(cretz): Document that this is only ever called with long today
        ICustomMetric<T>.ICounter CreateCounter<T>(
            string name, string? unit, string? description)
            where T : struct;

        ICustomMetric<T>.IHistogram CreateHistogram<T>(
            string name, string? unit, string? description)
            where T : struct;

        ICustomMetric<T>.IGauge CreateGauge<T>(
            string name, string? unit, string? description)
            where T : struct;

        // Document that tag object can only be string, long, double, or bool
        object CreateTags(
            object? appendFrom, IReadOnlyCollection<KeyValuePair<string, object>> tags);
    }
}