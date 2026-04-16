namespace Temporalio.Tests.Nexus;

using Temporalio.Nexus;
using Xunit;

public class TemporalOperationResultTests
{
    [Fact]
    public void Sync_SetsPropertiesCorrectly()
    {
        var result = TemporalOperationResult<string>.Sync("hello");

        Assert.True(result.IsSyncResult);
        Assert.Equal("hello", result.SyncValue);
        Assert.Null(result.AsyncToken);
    }

    [Fact]
    public void Sync_WithNull_Works()
    {
        var result = TemporalOperationResult<string>.Sync(null);

        Assert.True(result.IsSyncResult);
        Assert.Null(result.SyncValue);
        Assert.Null(result.AsyncToken);
    }

    [Fact]
    public void Sync_WithValueType_Works()
    {
        var result = TemporalOperationResult<int>.Sync(42);

        Assert.True(result.IsSyncResult);
        Assert.Equal(42, result.SyncValue);
        Assert.Null(result.AsyncToken);
    }

    [Fact]
    public void Async_SetsPropertiesCorrectly()
    {
        var result = TemporalOperationResult<string>.Async("some-token");

        Assert.False(result.IsSyncResult);
        Assert.Null(result.SyncValue);
        Assert.Equal("some-token", result.AsyncToken);
    }

    [Fact]
    public void Async_WithEmptyToken_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => TemporalOperationResult<string>.Async(string.Empty));
    }

    [Fact]
    public void Async_WithNullToken_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => TemporalOperationResult<string>.Async(null!));
    }
}
