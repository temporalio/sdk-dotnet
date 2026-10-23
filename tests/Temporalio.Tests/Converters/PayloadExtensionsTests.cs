namespace Temporalio.Tests.Converters;

using System;
using System.Linq;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using Xunit;
using Xunit.Abstractions;

public class PayloadExtensionsTests : TestBase
{
    public PayloadExtensionsTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public void ReplaceWith_EveryFieldSet_LeavesNothingOfTheOriginal()
    {
        // ReplaceWith clears each field by name, so a field added to Payload must be added there
        // and populated below before this passes.
        Assert.Equal(
            new[] { "data", "external_payloads", "metadata" },
            Payload.Descriptor.Fields.InDeclarationOrder()
                .Select(f => f.Name)
                .OrderBy(name => name, StringComparer.Ordinal));
        var payload = Populated("old");
        var replacement = Populated("new");

        payload.ReplaceWith(replacement);

        Assert.NotSame(replacement, payload);
        Assert.Equal(replacement, payload);
    }

    private static Payload Populated(string tag) => new()
    {
        Metadata = { [$"{tag}-key"] = ByteString.CopyFromUtf8(tag) },
        Data = ByteString.CopyFromUtf8(tag),
        ExternalPayloads = { new Payload.Types.ExternalPayloadDetails { SizeBytes = tag.Length } },
    };
}
