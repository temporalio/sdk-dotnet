namespace Temporalio.Tests.Nexus;

using System.Text;
using System.Text.Json;
using Temporalio.Nexus;
using Xunit;

public class NexusActivityExecutionHandleTests
{
    [Fact]
    public void ToToken_RoundTrips()
    {
        var handle = new NexusActivityExecutionHandle("my-namespace", "my-activity-id", 0);
        var token = handle.ToToken();
        var decoded = NexusActivityExecutionHandle.FromToken(token);

        Assert.Equal("my-namespace", decoded.Namespace);
        Assert.Equal("my-activity-id", decoded.ActivityId);
        Assert.Equal(0, decoded.Version);
    }

    [Fact]
    public void ToToken_UsesBase64Url_NoPadding()
    {
        var token = new NexusActivityExecutionHandle("my-ns", "my-aid", 0).ToToken();

        Assert.DoesNotContain("+", token);
        Assert.DoesNotContain("/", token);
        Assert.DoesNotContain("=", token);
    }

    [Fact]
    public void ToToken_JsonUsesCorrectKeys()
    {
        var token = new NexusActivityExecutionHandle("ns", "aid", 0).ToToken();
        var json = Encoding.UTF8.GetString(NexusWorkflowRunHandle.Base64UrlDecode(token));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(
            NexusActivityExecutionHandle.ActivityExecutionOperationTokenType,
            root.GetProperty("t").GetInt32());
        Assert.Equal("ns", root.GetProperty("ns").GetString());
        Assert.Equal("aid", root.GetProperty("aid").GetString());
    }

    [Fact]
    public void FromToken_RejectsWorkflowRunToken()
    {
        // A workflow-run token (t=1) must not parse as activity-execution.
        var wfToken = new NexusWorkflowRunHandle("ns", "wid", 0).ToToken();
        Assert.Throws<ArgumentException>(() => NexusActivityExecutionHandle.FromToken(wfToken));
    }

    [Fact]
    public void FromToken_RejectsMissingActivityId()
    {
        var json = """{"t":4,"ns":"ns"}""";
        var token = NexusWorkflowRunHandle.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        Assert.Throws<ArgumentException>(() => NexusActivityExecutionHandle.FromToken(token));
    }

    [Fact]
    public void FromToken_RejectsUnsupportedVersion()
    {
        var json = """{"t":4,"ns":"ns","aid":"aid","v":1}""";
        var token = NexusWorkflowRunHandle.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        Assert.Throws<ArgumentException>(() => NexusActivityExecutionHandle.FromToken(token));
    }

    [Fact]
    public void FromToken_RejectsInvalidBase64()
    {
        Assert.Throws<ArgumentException>(
            () => NexusActivityExecutionHandle.FromToken("!!!invalid!!!"));
    }

    [Fact]
    public void LoadTokenType_DetectsActivityToken()
    {
        var token = new NexusActivityExecutionHandle("ns", "aid", 0).ToToken();
        Assert.Equal(
            NexusActivityExecutionHandle.ActivityExecutionOperationTokenType,
            NexusWorkflowRunHandle.LoadTokenType(token));
    }

    [Fact]
    public void LoadTokenType_DetectsWorkflowToken()
    {
        var token = new NexusWorkflowRunHandle("ns", "wid", 0).ToToken();
        Assert.Equal(
            NexusWorkflowRunHandle.WorkflowRunOperationTokenType,
            NexusWorkflowRunHandle.LoadTokenType(token));
    }

    [Fact]
    public void LoadTokenType_RejectsEmpty()
    {
        Assert.Throws<ArgumentException>(() => NexusWorkflowRunHandle.LoadTokenType(string.Empty));
    }

    [Fact]
    public void LoadTokenType_RejectsMissingType()
    {
        var json = """{"ns":"ns","wid":"wid"}""";
        var token = NexusWorkflowRunHandle.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        Assert.Throws<ArgumentException>(() => NexusWorkflowRunHandle.LoadTokenType(token));
    }

    [Fact]
    public void ToToken_SpecialCharactersRoundTrip()
    {
        var handle = new NexusActivityExecutionHandle("ns/with+special", "aid?id=1&foo=bar", 0);
        var decoded = NexusActivityExecutionHandle.FromToken(handle.ToToken());

        Assert.Equal("ns/with+special", decoded.Namespace);
        Assert.Equal("aid?id=1&foo=bar", decoded.ActivityId);
    }
}
