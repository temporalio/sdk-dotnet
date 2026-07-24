namespace Temporalio.Tests.Nexus;

using System.Text;
using System.Text.Json;
using Temporalio.Nexus;
using Xunit;

public class NexusActivityExecutionTokenTests
{
    [Fact]
    public void Build_RoundTrips()
    {
        var token = NexusActivityExecutionToken.Create("my-namespace", "my-activity-id", "my-run-id");
        var decoded = NexusActivityExecutionToken.Parse(token);

        Assert.Equal("my-namespace", decoded.Namespace);
        Assert.Equal("my-activity-id", decoded.ActivityId);
        Assert.Equal("my-run-id", decoded.RunId);
        Assert.Null(decoded.Version);
    }

    [Fact]
    public void Build_RoundTripsWithoutRunId()
    {
        var token = NexusActivityExecutionToken.Create("ns", "aid", runId: null);
        var decoded = NexusActivityExecutionToken.Parse(token);

        Assert.Equal("ns", decoded.Namespace);
        Assert.Equal("aid", decoded.ActivityId);
        Assert.Null(decoded.RunId);
    }

    [Fact]
    public void Build_UsesBase64Url_NoPadding()
    {
        var token = NexusActivityExecutionToken.Create("my-ns", "my-aid", "my-rid");

        Assert.DoesNotContain("+", token);
        Assert.DoesNotContain("/", token);
        Assert.DoesNotContain("=", token);
    }

    [Fact]
    public void Build_JsonUsesCorrectKeys()
    {
        var token = NexusActivityExecutionToken.Create("ns", "aid", "rid");
        var json = Encoding.UTF8.GetString(NexusWorkflowRunHandle.Base64UrlDecode(token));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(
            NexusActivityExecutionToken.OperationTokenType,
            root.GetProperty("t").GetInt32());
        Assert.Equal("ns", root.GetProperty("ns").GetString());
        Assert.Equal("aid", root.GetProperty("aid").GetString());
        Assert.Equal("rid", root.GetProperty("rid").GetString());
    }

    [Fact]
    public void Parse_RejectsWorkflowRunToken()
    {
        // A workflow-run token (t=1) must not parse as activity-execution.
        var wfToken = new NexusWorkflowRunHandle("ns", "wid", 0).ToToken();
        Assert.Throws<ArgumentException>(() => NexusActivityExecutionToken.Parse(wfToken));
    }

    [Fact]
    public void Parse_RejectsMissingActivityId()
    {
        var json = """{"t":2,"ns":"ns"}""";
        var token = NexusWorkflowRunHandle.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        Assert.Throws<ArgumentException>(() => NexusActivityExecutionToken.Parse(token));
    }

    [Fact]
    public void Parse_RejectsUnsupportedVersion()
    {
        var json = """{"t":2,"ns":"ns","aid":"aid","v":1}""";
        var token = NexusWorkflowRunHandle.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        Assert.Throws<ArgumentException>(() => NexusActivityExecutionToken.Parse(token));
    }

    [Fact]
    public void Parse_RejectsInvalidBase64()
    {
        Assert.Throws<ArgumentException>(
            () => NexusActivityExecutionToken.Parse("!!!invalid!!!"));
    }

    [Fact]
    public void LoadTokenType_DetectsActivityToken()
    {
        var token = NexusActivityExecutionToken.Create("ns", "aid", "rid");
        Assert.Equal(
            NexusActivityExecutionToken.OperationTokenType,
            NexusWorkflowRunHandle.ParseTokenType(token));
    }

    [Fact]
    public void LoadTokenType_DetectsWorkflowToken()
    {
        var token = new NexusWorkflowRunHandle("ns", "wid", 0).ToToken();
        Assert.Equal(
            NexusWorkflowRunHandle.WorkflowRunOperationTokenType,
            NexusWorkflowRunHandle.ParseTokenType(token));
    }

    [Fact]
    public void LoadTokenType_RejectsEmpty()
    {
        Assert.Throws<ArgumentException>(() => NexusWorkflowRunHandle.ParseTokenType(string.Empty));
    }

    [Fact]
    public void LoadTokenType_RejectsMissingType()
    {
        var json = """{"ns":"ns","wid":"wid"}""";
        var token = NexusWorkflowRunHandle.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        Assert.Throws<ArgumentException>(() => NexusWorkflowRunHandle.ParseTokenType(token));
    }

    [Fact]
    public void Build_SpecialCharactersRoundTrip()
    {
        var token = NexusActivityExecutionToken.Create("ns/with+special", "aid?id=1&foo=bar", "rid/with+special");
        var decoded = NexusActivityExecutionToken.Parse(token);

        Assert.Equal("ns/with+special", decoded.Namespace);
        Assert.Equal("aid?id=1&foo=bar", decoded.ActivityId);
        Assert.Equal("rid/with+special", decoded.RunId);
    }
}
