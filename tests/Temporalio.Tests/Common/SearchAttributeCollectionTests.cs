namespace Temporalio.Tests.Common;

using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Common;
using Xunit;

public class SearchAttributeCollectionTests
{
    [Fact]
    public void FromProto_KeywordListWithNullElement_DoesNotThrow()
    {
        // A KeywordList payload whose JSON array contains a null element. The
        // server (or another SDK) can produce this, and decoding it should not
        // throw "Unexpected search attribute kind Null".
        var proto = new SearchAttributes
        {
            IndexedFields =
            {
                ["MyList"] = new Payload
                {
                    Metadata =
                    {
                        ["encoding"] = ByteString.CopyFromUtf8("json/plain"),
                        ["type"] = ByteString.CopyFromUtf8("KeywordList"),
                    },
                    Data = ByteString.CopyFromUtf8("[\"a\", null, \"b\"]"),
                },
            },
        };

        var collection = SearchAttributeCollection.FromProto(proto);

        var key = SearchAttributeKey.CreateKeywordList("MyList");
        Assert.Equal(["a", "b"], collection.Get(key));
    }

    [Fact]
    public void ToProto_KeywordWithEmptyString_EncodesAsJsonEmptyString()
    {
        var key = SearchAttributeKey.CreateKeyword("MyKeyword");
        var collection = new SearchAttributeCollection.Builder()
            .Set(key, string.Empty)
            .ToSearchAttributeCollection();

        var proto = collection.ToProto();

        Assert.True(proto.IndexedFields.TryGetValue("MyKeyword", out var payload));
        Assert.Equal("json/plain", payload.Metadata["encoding"].ToStringUtf8());
        Assert.Equal("Keyword", payload.Metadata["type"].ToStringUtf8());
        Assert.Equal("\"\"", payload.Data.ToStringUtf8());

        // And the empty string survives a round-trip back through FromProto.
        var roundTripped = SearchAttributeCollection.FromProto(proto);
        Assert.Equal(string.Empty, roundTripped.Get(key));
    }
}
