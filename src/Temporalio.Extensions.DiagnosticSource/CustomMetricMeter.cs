using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using Temporalio.Runtime;
using Temporalio.Workflows;

namespace Temporalio.Extensions.DiagnosticSource
{
    /// <summary>
    /// Implementation of <see cref="ICustomMetricMeter" /> for a <see cref="Meter" /> that can be
    /// set on <see cref="MetricsOptions.CustomMetricMeter" /> to record metrics to the meter.
    /// </summary>
    /// <remarks>
    /// By default all histograms are set as a <c>long</c> of milliseconds unless
    /// <see cref="MetricsOptions.CustomMetricMeterOptions"/> is set to <c>FloatSeconds</c>.
    /// Similarly, if the unit for a histogram is "duration", it is changed to "ms" unless that same
    /// setting is set, at which point the unit is changed to "s".
    /// </remarks>
    public class CustomMetricMeter : ICustomMetricMeter
    {
        private readonly bool disableWorkflowTracingEventListener;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomMetricMeter" /> class.
        /// </summary>
        /// <param name="meter">Meter to back this custom meter implementation with.</param>
        /// <param name="disableWorkflowTracingEventListener">
        /// When false, the default, calls to .NET meter from inside a workflow are subject to the
        /// event listener which catch non-deterministic thread, timer, and/or default task
        /// scheduler use. Some .NET meter adapter implementations, such as
        /// https://github.com/prometheus-net/prometheus-net with its "managed lease handles", do
        /// non-deterministic things. So setting this to true will surround in-workflow meter calls
        /// with
        /// <see cref="Workflow.Unsafe.WithTracingEventListenerDisabled{T}(Func{T})"/>.
        /// </param>
        public CustomMetricMeter(
            Meter meter,
            bool disableWorkflowTracingEventListener = false)
        {
            Meter = meter;
            this.disableWorkflowTracingEventListener = disableWorkflowTracingEventListener;
        }

        /// <summary>
        /// Gets the underlying meter for this custom meter.
        /// </summary>
        public Meter Meter { get; private init; }

        /// <inheritdoc />
        public ICustomMetricCounter<T> CreateCounter<T>(
            string name, string? unit, string? description)
            where T : struct =>
            new CustomMetricCounter<T>(
                WithUnderlyingMeter(m => m.CreateCounter<T>(name, unit, description)),
                disableWorkflowTracingEventListener);

        /// <inheritdoc />
        public ICustomMetricHistogram<T> CreateHistogram<T>(
            string name, string? unit, string? description)
            where T : struct
        {
            // Have to convert TimeSpan to something .NET meter can work with. For this to even
            // happen, a user would have had to set custom options to report as time span.
            if (typeof(T) == typeof(TimeSpan))
            {
                // If unit is "duration", change to "ms since we're converting here
                if (unit == "duration")
                {
                    unit = "ms";
                }
                return (new CustomMetricHistogramTimeSpan(
                    WithUnderlyingMeter(m => m.CreateHistogram<long>(name, unit, description)),
                    disableWorkflowTracingEventListener) as ICustomMetricHistogram<T>)!;
            }
            return new CustomMetricHistogram<T>(
                WithUnderlyingMeter(m => m.CreateHistogram<T>(name, unit, description)),
                disableWorkflowTracingEventListener);
        }

        /// <inheritdoc />
        public ICustomMetricGauge<T> CreateGauge<T>(
            string name, string? unit, string? description)
            where T : struct
        {
            var gauge = new CustomMetricGauge<T>(name);
            WithUnderlyingMeter(m => m.CreateObservableGauge(name, gauge.Snapshot, unit, description));
            return gauge;
        }

        /// <inheritdoc />
        public object CreateTags(
            object? appendFrom, IReadOnlyCollection<KeyValuePair<string, object>> tags) =>
            new Tags(tags, appendFrom);

        private T WithUnderlyingMeter<T>(Func<Meter, T> fn)
        {
            // If we're in a workflow and they have disabled tracing event listener, run under that
            if (Workflow.InWorkflow && disableWorkflowTracingEventListener)
            {
                return Workflow.Unsafe.WithTracingEventListenerDisabled(() => fn(Meter));
            }
            return fn(Meter);
        }

        private abstract class CustomMetric<T>
        {
            private readonly T underlying;
            private readonly bool disableWorkflowTracingEventListener;

            protected CustomMetric(T underlying, bool disableWorkflowTracingEventListener)
            {
                this.underlying = underlying;
                this.disableWorkflowTracingEventListener = disableWorkflowTracingEventListener;
            }

            protected void WithUnderlyingMetric(Action<T> fn)
            {
                // If we're in a workflow and they have disabled tracing event listener, run under that
                if (Workflow.InWorkflow && disableWorkflowTracingEventListener)
                {
                    Workflow.Unsafe.WithTracingEventListenerDisabled(() => fn(underlying));
                }
                else
                {
                    fn(underlying);
                }
            }
        }

        private sealed class CustomMetricCounter<T> : CustomMetric<Counter<T>>, ICustomMetricCounter<T>
            where T : struct
        {
            internal CustomMetricCounter(Counter<T> underlying, bool disableWorkflowTracingEventListener)
                : base(underlying, disableWorkflowTracingEventListener)
            {
            }

            public void Add(T value, object tags) =>
                WithUnderlyingMetric(m => m.Add(value, ((Tags)tags).TagList));
        }

        private sealed class CustomMetricHistogram<T> : CustomMetric<Histogram<T>>, ICustomMetricHistogram<T>
            where T : struct
        {
            internal CustomMetricHistogram(Histogram<T> underlying, bool disableWorkflowTracingEventListener)
                : base(underlying, disableWorkflowTracingEventListener)
            {
            }

            public void Record(T value, object tags) =>
                WithUnderlyingMetric(m => m.Record(value, ((Tags)tags).TagList));
        }

        private sealed class CustomMetricHistogramTimeSpan : CustomMetric<Histogram<long>>, ICustomMetricHistogram<TimeSpan>
        {
            internal CustomMetricHistogramTimeSpan(Histogram<long> underlying, bool disableWorkflowTracingEventListener)
                : base(underlying, disableWorkflowTracingEventListener)
            {
            }

            public void Record(TimeSpan value, object tags) =>
                WithUnderlyingMetric(m => m.Record((long)value.TotalMilliseconds, ((Tags)tags).TagList));
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

            // Used as callback for ObservableGauge
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