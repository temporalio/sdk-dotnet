namespace Temporalio.Tests.Runtime;

using System;
using System.Collections.Generic;
using System.Text;
using Temporalio.Bridge;
using Temporalio.Runtime;
using Xunit;

public class OpenTelemetryOptionsTests
{
    [Fact]
    public void ToInteropOptions_DefaultAggregation_UsesExplicitBuckets()
    {
        using var scope = new Scope();
        var options = new OpenTelemetryOptions("http://127.0.0.1:4317").ToInteropOptions(scope);

        Assert.Equal(
            Bridge.Interop.TemporalCoreOpenTelemetryHistogramAggregation.ExplicitBuckets,
            options.histogram_aggregation);
        Assert.Equal(0u, options.exponential_histogram_max_size);
        Assert.Equal((byte)0, options.exponential_histogram_max_scale_set);
        Assert.Equal(UIntPtr.Zero, options.exponential_histogram_name_suffix.size);
    }

    [Fact]
    public unsafe void ToInteropOptions_Base2Exponential_MapsOptions()
    {
        using var scope = new Scope();
        var options = NewOptions(new() { MaxSize = 80, MaxScale = -10, NameSuffix = "_exp" }).
            ToInteropOptions(scope);

        Assert.Equal(
            Bridge.Interop.TemporalCoreOpenTelemetryHistogramAggregation.Base2Exponential,
            options.histogram_aggregation);
        Assert.Equal(80u, options.exponential_histogram_max_size);
        Assert.Equal((sbyte)-10, options.exponential_histogram_max_scale);
        Assert.Equal((byte)1, options.exponential_histogram_max_scale_set);
        Assert.Equal(
            "_exp",
            Encoding.UTF8.GetString(
                options.exponential_histogram_name_suffix.data,
                (int)options.exponential_histogram_name_suffix.size));
    }

    [Fact]
    public void ToInteropOptions_Base2ExponentialUnset_LeavesCoreDefaults()
    {
        using var scope = new Scope();
        var options = NewOptions(new()).ToInteropOptions(scope);

        Assert.Equal(
            Bridge.Interop.TemporalCoreOpenTelemetryHistogramAggregation.Base2Exponential,
            options.histogram_aggregation);
        Assert.Equal(0u, options.exponential_histogram_max_size);
        Assert.Equal((byte)0, options.exponential_histogram_max_scale_set);
        Assert.Equal(UIntPtr.Zero, options.exponential_histogram_name_suffix.size);
    }

    [Fact]
    public void ToInteropOptions_Base2ExponentialZeroScale_MarksScaleSet()
    {
        using var scope = new Scope();
        var options = NewOptions(new() { MaxScale = 0 }).ToInteropOptions(scope);

        Assert.Equal((sbyte)0, options.exponential_histogram_max_scale);
        Assert.Equal((byte)1, options.exponential_histogram_max_scale_set);
    }

    [Fact]
    public void ToInteropOptions_Base2Exponential_ValidatesOptions()
    {
        using var scope = new Scope();
        NewOptions(new() { MaxScale = 20 }).ToInteropOptions(scope);

        var exc = Assert.Throws<ArgumentException>(
            () => NewOptions(new() { MaxScale = -11 }).ToInteropOptions(scope));
        Assert.Contains("MaxScale must be from -10 to 20", exc.Message);

        exc = Assert.Throws<ArgumentException>(
            () => NewOptions(new() { MaxScale = 21 }).ToInteropOptions(scope));
        Assert.Contains("MaxScale must be from -10 to 20", exc.Message);

        exc = Assert.Throws<ArgumentException>(
            () => NewOptions(new() { MaxSize = 0 }).ToInteropOptions(scope));
        Assert.Contains("MaxSize must be positive", exc.Message);

        exc = Assert.Throws<ArgumentException>(
            () => NewOptions(new() { NameSuffix = string.Empty }).ToInteropOptions(scope));
        Assert.Contains("NameSuffix must not be empty", exc.Message);

        exc = Assert.Throws<ArgumentException>(
            () => NewOptions(
                new(),
                new Dictionary<string, IReadOnlyCollection<double>>
                {
                    ["custom_histogram"] = new[] { 5d, 6, 7 },
                }).ToInteropOptions(scope));
        Assert.Contains("HistogramBucketOverrides cannot be combined", exc.Message);
    }

    private static OpenTelemetryOptions NewOptions(
        OpenTelemetryHistogramAggregation.Base2Exponential aggregation,
        IReadOnlyDictionary<string, IReadOnlyCollection<double>>? bucketOverrides = null) =>
        new("http://127.0.0.1:4317")
        {
            HistogramAggregation = aggregation,
            HistogramBucketOverrides = bucketOverrides,
        };
}
