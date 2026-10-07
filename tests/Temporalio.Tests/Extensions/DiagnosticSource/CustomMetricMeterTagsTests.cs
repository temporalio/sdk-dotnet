namespace Temporalio.Tests.Extensions.DiagnosticSource;

using System.Diagnostics.Metrics;
using Temporalio.Extensions.DiagnosticSource;
using Xunit;
using Xunit.Abstractions;

public class CustomMetricMeterTagsTests : TestBase
{
    public CustomMetricMeterTagsTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public void CreateTags_AppendedTagWithExistingKey_OverridesExistingValue()
    {
        using var meter = new Meter("test-tags-meter");
        var customMeter = new CustomMetricMeter(meter);

        var original = customMeter.CreateTags(null, new KeyValuePair<string, object>[]
        {
            new("foo", "bar"),
            new("baz", 1234L),
        });
        var appended = customMeter.CreateTags(original, new KeyValuePair<string, object>[]
        {
            new("foo", "qux"),
        });

        var expected = customMeter.CreateTags(null, new KeyValuePair<string, object>[]
        {
            new("baz", 1234L),
            new("foo", "qux"),
        });
        Assert.Equal(expected, appended);
        Assert.Equal(expected.GetHashCode(), appended.GetHashCode());
    }

    [Fact]
    public void CreateTags_DuplicateKeysInNewTags_LastValueWins()
    {
        using var meter = new Meter("test-tags-meter");
        var customMeter = new CustomMetricMeter(meter);

        var tags = customMeter.CreateTags(null, new KeyValuePair<string, object>[]
        {
            new("foo", "first"),
            new("foo", "second"),
        });

        var expected = customMeter.CreateTags(null, new KeyValuePair<string, object>[]
        {
            new("foo", "second"),
        });
        Assert.Equal(expected, tags);
    }
}
