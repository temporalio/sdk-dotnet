using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using Temporalio.Runtime;

namespace Temporalio.Extensions.DiagnosticSource
{
    /// <summary>
    /// Implementation of <see cref="ICustomMetricMeter" /> for a <see cref="Meter" /> that can be
    /// set on <see cref="MetricsOptions.CustomMetricMeter" /> to record metrics to the meter.
    /// </summary>
    public class CustomMetricMeter : ICustomMetricMeter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CustomMetricMeter" /> class.
        /// </summary>
        /// <param name="meter">Meter to back this custom meter implementation with.</param>
        public CustomMetricMeter(Meter meter) => Meter = meter;

        /// <summary>
        /// Gets the underlying meter for this custom meter.
        /// </summary>
        public Meter Meter { get; private init; }

        /// <inheritdoc />
        public ICustomMetricCounter<T> CreateCounter<T>(
            string name, string? unit, string? description)
            where T : struct =>
            new CustomMetricCounter<T>(Meter.CreateCounter<T>(name, unit, description));

        /// <inheritdoc />
        public ICustomMetricHistogram<T> CreateHistogram<T>(
            string name, string? unit, string? description)
            where T : struct =>
            new CustomMetricHistogram<T>(Meter.CreateHistogram<T>(name, unit, description));

        /// <inheritdoc />
        public ICustomMetricGauge<T> CreateGauge<T>(
            string name, string? unit, string? description)
            where T : struct
        {
            var gauge = new CustomMetricGauge<T>(name);
            Meter.CreateObservableGauge(name, gauge.Snapshot, unit, description);
            return gauge;
        }

        /// <inheritdoc />
        public object CreateTags(
            object? appendFrom, IReadOnlyCollection<KeyValuePair<string, object>> tags) =>
            new Tags(tags, appendFrom);

        private sealed class CustomMetricCounter<T> : ICustomMetricCounter<T>
            where T : struct
        {
            private readonly Counter<T> underlying;

            internal CustomMetricCounter(Counter<T> underlying) => this.underlying = underlying;

            public void Add(T value, object tags) =>
                underlying.Add(value, ((Tags)tags).TagList);
        }

        private sealed class CustomMetricHistogram<T> : ICustomMetricHistogram<T>
            where T : struct
        {
            private readonly Histogram<T> underlying;

            internal CustomMetricHistogram(Histogram<T> underlying) => this.underlying = underlying;

            public void Record(T value, object tags) =>
                underlying.Record(value, ((Tags)tags).TagList);
        }

#pragma warning disable CA1001 // We are disposing the lock on destruction since this can't be disposable
        private sealed class CustomMetricGauge<T> : ICustomMetricGauge<T>
#pragma warning restore CA1001
            where T : struct
        {
            private readonly string name;
            // We must dedupe based on tag set. We considered several designs including regular
            // dictionary, blocking collection or concurrent queue w/ read time dedupe, and others
            // and it was clearest to use a concurrent dictionary for collection but lock it while
            // collecting metrics. Iterating a concurrent dictionary does not guarantee duplicates
            // will be prevented, so we must lock it while iterating during collection.
            private readonly ConcurrentDictionary<Tags, Measurement<T>> pending = new();
            private readonly ReaderWriterLockSlim pendingLock = new();

            internal CustomMetricGauge(string name) => this.name = name;

            ~CustomMetricGauge() => pendingLock.Dispose();

            public void Set(T value, object tags)
            {
                // We need to support an extreme max here just to prevent unbounded memory growth.
                // We do not want to require a logger for this, so at some extreme value we will
                // just write to stderr and drop.
                if (pending.Count > 50000)
                {
                    Console.Error.WriteLine($"Dropping gauge metric {name} since cardinality has grown over 50k");
                    return;
                }
                // We are grabbing read lock because this is ok to happen concurrently even though
                // technically we are doing more than reading
                var tagsObj = (Tags)tags;
                var measurement = new Measurement<T>(value, tagsObj.TagList);
                pendingLock.EnterReadLock();
                try
                {
                    pending[tagsObj] = measurement;
                }
                finally
                {
                    pendingLock.ExitReadLock();
                }
            }

            public List<Measurement<T>> Snapshot()
            {
                pendingLock.EnterWriteLock();
                try
                {
                    return pending.Values.ToList();
                }
                finally
                {
                    pendingLock.ExitWriteLock();
                }
            }
        }

        private sealed class Tags : IEquatable<Tags>
        {
            private int? hashCode;

            public Tags(
                IReadOnlyCollection<KeyValuePair<string, object>> newTags, object? appendFrom)
            {
                // Build sorted tag array
                IEnumerable<KeyValuePair<string, object>> tags;
                if (appendFrom is Tags appendFromTags)
                {
                    tags = appendFromTags.TagList.Concat(newTags);
                }
                else
                {
                    tags = newTags;
                }
                TagList = new(new(tags.ToDictionary(
                    kv => kv.Key, kv => (object?)kv.Value).OrderBy(kv => kv.Key).ToArray()));
            }

            public TagList TagList { get; private init; }

            public override bool Equals(object? obj) =>
                obj is Tags tagsObj && Equals(tagsObj);

            public bool Equals(Tags other)
            {
                if (other.TagList.Count != TagList.Count)
                {
                    return false;
                }
                for (int i = 0; i < TagList.Count; i++)
                {
                    var kv = TagList[i];
                    var otherKV = other.TagList[i];
                    if (kv.Key != otherKV.Key || !kv.Value!.Equals(otherKV.Value))
                    {
                        return false;
                    }
                }
                return true;
            }

            public override int GetHashCode()
            {
                // Values are already sorted, so we'll just xor the tuples' hash codes. We don't
                // care about the race of this running multiple times at once.
                hashCode ??= TagList.Aggregate(0, (acc, kv) => acc ^ (kv.Key, kv.Value).GetHashCode());
                return hashCode.Value;
            }
        }
    }
}