namespace Temporalio.Tests.Nexus;

using System.Text;
using System.Text.Json;
using Temporalio.Nexus;
using Xunit;

public class NexusWorkflowStartHelperTests
{
    [Fact]
    public void GenerateToken_ProducesValidBase64Url()
    {
        var token = NexusWorkflowStartHelper.GenerateToken("my-ns", "my-wf");

        Assert.DoesNotContain("+", token);
        Assert.DoesNotContain("/", token);
        Assert.DoesNotContain("=", token);
    }

    [Fact]
    public void GenerateToken_ContainsCorrectFields()
    {
        var token = NexusWorkflowStartHelper.GenerateToken("my-ns", "my-wf");
        var json = Encoding.UTF8.GetString(NexusWorkflowStartHelper.Base64UrlDecode(token));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("t").GetInt32());
        Assert.Equal("my-ns", root.GetProperty("ns").GetString());
        Assert.Equal("my-wf", root.GetProperty("wid").GetString());
    }

    [Fact]
    public void GenerateToken_CrossCompatibleWithNexusWorkflowRunHandle()
    {
        var token = NexusWorkflowStartHelper.GenerateToken("default", "test-wf");

        // Verify existing NexusWorkflowRunHandle can decode tokens from the helper
        var handle = NexusWorkflowRunHandle.FromToken(token);
        Assert.Equal("default", handle.Namespace);
        Assert.Equal("test-wf", handle.WorkflowId);
        Assert.Equal(0, handle.Version);
    }

    [Fact]
    public void ParseToken_RoundTrips()
    {
        var token = NexusWorkflowStartHelper.GenerateToken("my-ns", "my-wf");
        var parsed = NexusWorkflowStartHelper.ParseToken(token);

        Assert.Equal("my-ns", parsed.Namespace);
        Assert.Equal("my-wf", parsed.WorkflowId);
        Assert.Equal(1, parsed.Type);
    }

    [Fact]
    public void ParseToken_RejectsInvalidBase64()
    {
        Assert.Throws<ArgumentException>(() => NexusWorkflowStartHelper.ParseToken("!!!invalid!!!"));
    }

    [Fact]
    public void ParseToken_RejectsInvalidJson()
    {
        var token = NexusWorkflowStartHelper.Base64UrlEncode(Encoding.UTF8.GetBytes("not json"));
        Assert.Throws<ArgumentException>(() => NexusWorkflowStartHelper.ParseToken(token));
    }

    [Fact]
    public void ParseToken_RejectsUnsupportedVersion()
    {
        var json = """{"t":1,"ns":"ns","wid":"wid","v":99}""";
        var token = NexusWorkflowStartHelper.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        Assert.Throws<ArgumentException>(() => NexusWorkflowStartHelper.ParseToken(token));
    }

    [Fact]
    public void ParseToken_AcceptsUnknownTokenType()
    {
        // ParseToken should not reject unknown token types — that's for the handler to decide
        var json = """{"t":99,"ns":"ns","wid":"wid"}""";
        var token = NexusWorkflowStartHelper.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        var parsed = NexusWorkflowStartHelper.ParseToken(token);
        Assert.Equal(99, parsed.Type);
    }

    [Fact]
    public void GenerateToken_SpecialCharactersRoundTrip()
    {
        var token = NexusWorkflowStartHelper.GenerateToken("ns/with+special", "wf?id=1&foo=bar");
        var parsed = NexusWorkflowStartHelper.ParseToken(token);

        Assert.Equal("ns/with+special", parsed.Namespace);
        Assert.Equal("wf?id=1&foo=bar", parsed.WorkflowId);
    }

    [Fact]
    public void NexusWorkflowRunHandle_CanDecodeHelperToken_AndViceVersa()
    {
        // Helper token -> NexusWorkflowRunHandle
        var helperToken = NexusWorkflowStartHelper.GenerateToken("ns1", "wf1");
        var handle = NexusWorkflowRunHandle.FromToken(helperToken);
        Assert.Equal("ns1", handle.Namespace);
        Assert.Equal("wf1", handle.WorkflowId);

        // NexusWorkflowRunHandle token -> Helper parse
        var handleToken = new NexusWorkflowRunHandle("ns2", "wf2", 0).ToToken();
        var parsed = NexusWorkflowStartHelper.ParseToken(handleToken);
        Assert.Equal("ns2", parsed.Namespace);
        Assert.Equal("wf2", parsed.WorkflowId);
        Assert.Equal(1, parsed.Type);
    }
}
