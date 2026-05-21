namespace Temporalio.Tests.Nexus;

using System.Text;
using System.Text.Json;
using Temporalio.Nexus;
using Xunit;

public class NexusWorkflowStartHelperTests
{
    [Fact]
    public void ToToken_ProducesValidBase64Url()
    {
        var token = new NexusWorkflowRunHandle("my-ns", "my-wf", 0).ToToken();

        Assert.DoesNotContain("+", token);
        Assert.DoesNotContain("/", token);
        Assert.DoesNotContain("=", token);
    }

    [Fact]
    public void ToToken_ContainsCorrectFields()
    {
        var token = new NexusWorkflowRunHandle("my-ns", "my-wf", 0).ToToken();
        var json = Encoding.UTF8.GetString(NexusWorkflowRunHandle.Base64UrlDecode(token));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("t").GetInt32());
        Assert.Equal("my-ns", root.GetProperty("ns").GetString());
        Assert.Equal("my-wf", root.GetProperty("wid").GetString());
    }

    [Fact]
    public void ParseToken_RoundTrips()
    {
        var token = new NexusWorkflowRunHandle("my-ns", "my-wf", 0).ToToken();
        var parsed = NexusWorkflowRunHandle.ParseToken(token);

        Assert.Equal("my-ns", parsed.Namespace);
        Assert.Equal("my-wf", parsed.WorkflowId);
        Assert.Equal(1, parsed.Type);
    }

    [Fact]
    public void ParseToken_RejectsInvalidBase64()
    {
        Assert.Throws<ArgumentException>(() => NexusWorkflowRunHandle.ParseToken("!!!invalid!!!"));
    }

    [Fact]
    public void ParseToken_RejectsInvalidJson()
    {
        var token = NexusWorkflowRunHandle.Base64UrlEncode(Encoding.UTF8.GetBytes("not json"));
        Assert.Throws<ArgumentException>(() => NexusWorkflowRunHandle.ParseToken(token));
    }

    [Fact]
    public void ParseToken_RejectsUnsupportedVersion()
    {
        var json = """{"t":1,"ns":"ns","wid":"wid","v":99}""";
        var token = NexusWorkflowRunHandle.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        Assert.Throws<ArgumentException>(() => NexusWorkflowRunHandle.ParseToken(token));
    }

    [Fact]
    public void ParseToken_AcceptsUnknownTokenType()
    {
        // ParseToken should not reject unknown token types — that's for the handler to decide
        var json = """{"t":99,"ns":"ns","wid":"wid"}""";
        var token = NexusWorkflowRunHandle.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        var parsed = NexusWorkflowRunHandle.ParseToken(token);
        Assert.Equal(99, parsed.Type);
    }

    [Fact]
    public void ToToken_SpecialCharactersRoundTrip()
    {
        var token = new NexusWorkflowRunHandle("ns/with+special", "wf?id=1&foo=bar", 0).ToToken();
        var parsed = NexusWorkflowRunHandle.ParseToken(token);

        Assert.Equal("ns/with+special", parsed.Namespace);
        Assert.Equal("wf?id=1&foo=bar", parsed.WorkflowId);
    }

    [Fact]
    public void TemporalOperationResult_Sync_StoresValue()
    {
        var result = TemporalOperationResult<string>.Sync("hello");
        Assert.True(result.IsSyncResult);
        Assert.Equal("hello", result.SyncValue);
        Assert.Null(result.AsyncToken);
    }

    [Fact]
    public void TemporalOperationResult_Sync_AllowsDefault()
    {
        var result = TemporalOperationResult<string>.Sync(null);
        Assert.True(result.IsSyncResult);
        Assert.Null(result.SyncValue);
        Assert.Null(result.AsyncToken);
    }

    [Fact]
    public void TemporalOperationResult_Async_StoresToken()
    {
        var result = TemporalOperationResult<string>.Async("some-token");
        Assert.False(result.IsSyncResult);
        Assert.Equal("some-token", result.AsyncToken);
        Assert.Null(result.SyncValue);
    }

    [Fact]
    public void TemporalOperationResult_Async_RejectsNull()
    {
        Assert.Throws<ArgumentException>(() => TemporalOperationResult<string>.Async(null!));
    }

    [Fact]
    public void TemporalOperationResult_Async_RejectsEmpty()
    {
        Assert.Throws<ArgumentException>(() => TemporalOperationResult<string>.Async(string.Empty));
    }
}
