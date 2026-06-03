namespace Temporalio.Tests.Common;

using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Common;
using Xunit;

public class SearchAttributeCollectionTests
{
    private static readonly SearchAttributeKey<IReadOnlyCollection<string>> KeywordListKey =
        SearchAttributeKey.CreateKeywordList("my-keyword-list");

    [Fact]
    public void KeywordList_NullSetValue_Rejected()
    {
        Assert.Throws<ArgumentException>(() => KeywordListKey.ValueSet(null!));
        Assert.Throws<ArgumentException>(
            () => new SearchAttributeCollection.Builder()
                .Set(KeywordListKey, null!)
                .ToSearchAttributeCollection());
    }

    [Fact]
    public void KeywordList_NullElement_Rejected()
    {
        Assert.Throws<ArgumentException>(
            () => KeywordListKey.ValueSet(new[] { "keyword", null! }).ToUpsertPayload());
    }

    [Fact]
    public void KeywordList_Unset_SerializesWithoutTypeMetadata()
    {
        var payload = KeywordListKey.ValueUnset().ToUpsertPayload();

        Assert.Equal("binary/null", payload.Metadata["encoding"].ToStringUtf8());
        Assert.False(payload.Metadata.ContainsKey("type"));
    }

    [Fact]
    public void KeywordList_NullPayload_DecodesAsAbsent()
    {
        foreach (var payload in new[]
        {
            JsonNullPayload(false), BinaryNullPayload(true), JsonNullPayload(true),
        })
        {
            var attrs = SearchAttributeCollection.FromProto(new SearchAttributes()
            {
                IndexedFields = { [KeywordListKey.Name] = payload },
            });

            Assert.Empty(attrs);
            Assert.False(attrs.ContainsKey(KeywordListKey));
        }
    }

    private static Payload BinaryNullPayload(bool withType)
    {
        var payload = new Payload();
        payload.Metadata["encoding"] = ByteString.CopyFromUtf8("binary/null");
        if (withType)
        {
            payload.Metadata["type"] = ByteString.CopyFromUtf8("KeywordList");
        }
        return payload;
    }

    private static Payload JsonNullPayload(bool withType)
    {
        var payload = new Payload() { Data = ByteString.CopyFromUtf8("null") };
        payload.Metadata["encoding"] = ByteString.CopyFromUtf8("json/plain");
        if (withType)
        {
            payload.Metadata["type"] = ByteString.CopyFromUtf8("KeywordList");
        }
        return payload;
    }
}
