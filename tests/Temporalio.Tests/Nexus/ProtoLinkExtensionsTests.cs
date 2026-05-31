namespace Temporalio.Tests.Nexus;

using NexusRpc;
using Temporalio.Api.Common.V1;
using Temporalio.Nexus;
using Xunit;

public class ProtoLinkExtensionsTests
{
    [Fact]
    public void ActivityLink_ToNexusLink_BuildsExpectedUri()
    {
        var act = new Link.Types.Activity
        {
            Namespace = "my-ns",
            ActivityId = "my-aid",
            RunId = "my-run",
        };
        var nexusLink = act.ToNexusLink();

        Assert.Equal("temporal", nexusLink.Uri.Scheme);
        Assert.Equal(Link.Types.Activity.Descriptor.FullName, nexusLink.Type);
        Assert.Equal(
            "/namespaces/my-ns/activities/my-aid/my-run/details",
            nexusLink.Uri.AbsolutePath);
    }

    [Fact]
    public void ToActivity_ParsesServerStyleUri()
    {
        // Servers produce URIs in the host-less form `temporal:/namespaces/.../details`.
        var link = new NexusLink(
            new Uri("temporal:/namespaces/my-ns/activities/my-aid/my-run/details"),
            Link.Types.Activity.Descriptor.FullName);

        var act = link.ToActivity();
        Assert.Equal("my-ns", act.Namespace);
        Assert.Equal("my-aid", act.ActivityId);
        Assert.Equal("my-run", act.RunId);
    }

    [Fact]
    public void ToActivity_RejectsNonTemporalScheme()
    {
        var link = new NexusLink(
            new Uri("https://example/namespaces/ns/activities/aid/run/details"),
            Link.Types.Activity.Descriptor.FullName);
        Assert.Throws<ArgumentException>(() => link.ToActivity());
    }

    [Fact]
    public void ToActivity_RejectsBadPath()
    {
        var link = new NexusLink(
            new Uri("temporal:///namespaces/ns/workflows/wid/run/history"),
            Link.Types.Activity.Descriptor.FullName);
        Assert.Throws<ArgumentException>(() => link.ToActivity());
    }
}
