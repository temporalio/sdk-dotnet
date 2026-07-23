namespace Temporalio.Tests.Nexus;

using System.Linq;
using System.Text;
using System.Text.Json;
using Temporalio.Api.Common.V1;
using Temporalio.Nexus;
using Xunit;

public class NexusWorkflowUpdateHandleTests
{
    [Fact]
    public void ToToken_RoundTrips()
    {
        var handle = new NexusWorkflowUpdateHandle("my-ns", "my-wf", "my-run", "my-update", 0);
        var token = handle.ToToken();
        var decoded = NexusWorkflowUpdateHandle.FromToken(token);

        Assert.Equal("my-ns", decoded.Namespace);
        Assert.Equal("my-wf", decoded.WorkflowId);
        Assert.Equal("my-run", decoded.RunId);
        Assert.Equal("my-update", decoded.UpdateId);
        Assert.Equal(0, decoded.Version);
    }

    [Fact]
    public void ToToken_UsesBase64Url_NoPadding()
    {
        var token = new NexusWorkflowUpdateHandle("my-ns", "my-wf", "my-run", "my-update", 0).ToToken();

        Assert.DoesNotContain("+", token);
        Assert.DoesNotContain("/", token);
        Assert.DoesNotContain("=", token);
    }

    [Fact]
    public void ToToken_JsonUsesCorrectKeys()
    {
        var token = new NexusWorkflowUpdateHandle("ns", "wid", "rid", "uid", 0).ToToken();
        var json = Encoding.UTF8.GetString(NexusWorkflowRunHandle.Base64UrlDecode(token));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(
            NexusWorkflowRunHandle.UpdateWorkflowOperationTokenType,
            root.GetProperty("t").GetInt32());
        Assert.Equal("ns", root.GetProperty("ns").GetString());
        Assert.Equal("wid", root.GetProperty("wid").GetString());
        Assert.Equal("rid", root.GetProperty("rid").GetString());
        Assert.Equal("uid", root.GetProperty("uid").GetString());
        // Version must be omitted when zero.
        Assert.False(root.TryGetProperty("v", out _));
        // rid must precede uid in the serialized form.
        var order = string.Join(",", root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("ns,wid,t,rid,uid", order);
    }

    [Fact]
    public void ToToken_EmptyRunId_OmitsRid()
    {
        // Run ID is optional; an empty run ID must be omitted entirely rather than emitted as
        // "rid":"" (matching the Nexus operation token format spec).
        var token = new NexusWorkflowUpdateHandle("ns", "wid", string.Empty, "uid", 0).ToToken();
        var json = Encoding.UTF8.GetString(NexusWorkflowRunHandle.Base64UrlDecode(token));
        using var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.TryGetProperty("rid", out _));
    }

    [Fact]
    public void FromToken_DecodesGoWireFormat()
    {
        // A token as it would be encoded by another SDK (field names t/ns/wid/rid/uid).
        var json = """{"t":3,"ns":"ns","wid":"w","rid":"r","uid":"u"}""";
        var token = NexusWorkflowRunHandle.Base64UrlEncode(Encoding.UTF8.GetBytes(json));

        var handle = NexusWorkflowUpdateHandle.FromToken(token);

        Assert.Equal("ns", handle.Namespace);
        Assert.Equal("w", handle.WorkflowId);
        Assert.Equal("r", handle.RunId);
        Assert.Equal("u", handle.UpdateId);
    }

    [Fact]
    public void FromToken_RejectsWrongTokenType()
    {
        // Workflow-run token type (1) is not a valid update-workflow token.
        var json = """{"t":1,"ns":"ns","wid":"w"}""";
        var token = NexusWorkflowRunHandle.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        Assert.Throws<System.ArgumentException>(() => NexusWorkflowUpdateHandle.FromToken(token));
    }

    [Fact]
    public void FromToken_RejectsMissingNamespace()
    {
        var json = """{"t":3,"wid":"w","uid":"u"}""";
        var token = NexusWorkflowRunHandle.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        Assert.Throws<System.ArgumentException>(() => NexusWorkflowUpdateHandle.FromToken(token));
    }

    [Fact]
    public void FromToken_RejectsMissingWorkflowId()
    {
        var json = """{"t":3,"ns":"ns","uid":"u"}""";
        var token = NexusWorkflowRunHandle.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        Assert.Throws<System.ArgumentException>(() => NexusWorkflowUpdateHandle.FromToken(token));
    }

    [Fact]
    public void FromToken_RejectsMissingUpdateId()
    {
        var json = """{"t":3,"ns":"ns","wid":"w"}""";
        var token = NexusWorkflowRunHandle.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        Assert.Throws<System.ArgumentException>(() => NexusWorkflowUpdateHandle.FromToken(token));
    }

    [Fact]
    public void FromToken_RejectsUnsupportedVersion()
    {
        var json = """{"t":3,"ns":"ns","wid":"w","rid":"r","uid":"u","v":1}""";
        var token = NexusWorkflowRunHandle.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        Assert.Throws<System.ArgumentException>(() => NexusWorkflowUpdateHandle.FromToken(token));
    }

    [Fact]
    public void ToToken_SpecialCharactersInValues_RoundTrip()
    {
        var handle = new NexusWorkflowUpdateHandle(
            "ns/with+special", "wf?id=1&foo=bar", "run+/id", "upd id", 0);
        var decoded = NexusWorkflowUpdateHandle.FromToken(handle.ToToken());

        Assert.Equal("ns/with+special", decoded.Namespace);
        Assert.Equal("wf?id=1&foo=bar", decoded.WorkflowId);
        Assert.Equal("run+/id", decoded.RunId);
        Assert.Equal("upd id", decoded.UpdateId);
    }

    [Fact]
    public void WorkflowRunToken_OmitsUpdateFields()
    {
        // Regression: the workflow-run token must not gain rid/uid fields.
        var token = new NexusWorkflowRunHandle("ns", "wid", 0).ToToken();
        var json = Encoding.UTF8.GetString(NexusWorkflowRunHandle.Base64UrlDecode(token));
        using var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.TryGetProperty("rid", out _));
        Assert.False(doc.RootElement.TryGetProperty("uid", out _));
    }

    [Theory]
    [InlineData("", "wid", "uid")]
    [InlineData("ns", "", "uid")]
    [InlineData("ns", "wid", "")]
    public void ToToken_EmptyRequiredField_Throws(string ns, string wid, string uid)
    {
        // An empty namespace/workflow-ID/update-ID would produce a token that breaks server-side
        // dedup, so token generation must reject it (matching the Go/TS token generators).
        var handle = new NexusWorkflowUpdateHandle(ns, wid, "rid", uid, 0);
        Assert.Throws<System.ArgumentException>(() => handle.ToToken());
    }

    [Fact]
    public void ToToken_EmptyRunId_IsAllowed()
    {
        // Run ID is optional; only namespace/workflow-ID/update-ID are required.
        var token = new NexusWorkflowUpdateHandle("ns", "wid", string.Empty, "uid", 0).ToToken();
        Assert.NotEmpty(token);
    }

    [Fact]
    public void ToToken_WithRunId_EmitsRidAndRoundTrips()
    {
        var token = new NexusWorkflowUpdateHandle("ns", "wid", "my-run", "uid", 0).ToToken();
        var json = Encoding.UTF8.GetString(NexusWorkflowRunHandle.Base64UrlDecode(token));
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("rid", out var rid));
        Assert.Equal("my-run", rid.GetString());

        var decoded = NexusWorkflowUpdateHandle.FromToken(token);
        Assert.Equal("my-run", decoded.RunId);
    }

    [Fact]
    public void FromToken_ToleratesLegacyEmptyRid()
    {
        // A legacy token that still carries rid:"" must decode to an empty run ID.
        var json = """{"t":3,"ns":"ns","wid":"w","rid":"","uid":"u"}""";
        var token = NexusWorkflowRunHandle.Base64UrlEncode(Encoding.UTF8.GetBytes(json));

        var handle = NexusWorkflowUpdateHandle.FromToken(token);

        Assert.Equal(string.Empty, handle.RunId);
        Assert.Equal("u", handle.UpdateId);
    }

    [Fact]
    public void FromToken_ToleratesAbsentRid()
    {
        // A token that omits rid entirely must decode to an empty run ID.
        var json = """{"t":3,"ns":"ns","wid":"w","uid":"u"}""";
        var token = NexusWorkflowRunHandle.Base64UrlEncode(Encoding.UTF8.GetBytes(json));

        var handle = NexusWorkflowUpdateHandle.FromToken(token);

        Assert.Equal(string.Empty, handle.RunId);
        Assert.Equal("u", handle.UpdateId);
    }

    [Fact]
    public void CommonLink_ToNexusLink_UnsetOneof_ReturnsNull()
    {
        // A link whose oneof is unset (neither workflow-event nor workflow) must not dereference a
        // null variant; it returns null so callers can skip it.
        var link = new Link().ToNexusLink();

        Assert.Null(link);
    }

    [Fact]
    public void WorkflowLink_ToNexusLink_BuildsHistoryUri()
    {
        var workflow = new Link.Types.Workflow
        {
            Namespace = "ns",
            WorkflowId = "wid",
            RunId = "rid",
        };
        var link = workflow.ToNexusLink();

        Assert.Equal("temporal", link.Uri.Scheme);
        Assert.Equal("/namespaces/ns/workflows/wid/rid/history", link.Uri.AbsolutePath);
        Assert.Equal(Link.Types.Workflow.Descriptor.FullName, link.Type);
    }

    [Fact]
    public void CommonLink_ToNexusLink_PrefersWorkflowEvent()
    {
        var common = new Link
        {
            WorkflowEvent = new()
            {
                Namespace = "ns",
                WorkflowId = "wid",
                RunId = "rid",
                EventRef = new() { EventId = 1 },
            },
        };
        var link = common.ToNexusLink();

        Assert.NotNull(link);
        Assert.Equal(Link.Types.WorkflowEvent.Descriptor.FullName, link.Type);
    }

    [Fact]
    public void CommonLink_ToNexusLink_FallsBackToWorkflow()
    {
        // No history event (e.g. a rejected update) — falls back to the workflow link.
        var common = new Link
        {
            Workflow = new()
            {
                Namespace = "ns",
                WorkflowId = "wid",
                RunId = "rid",
            },
        };
        var link = common.ToNexusLink();

        Assert.NotNull(link);
        Assert.Equal(Link.Types.Workflow.Descriptor.FullName, link.Type);
        Assert.Equal("/namespaces/ns/workflows/wid/rid/history", link.Uri.AbsolutePath);
    }
}
