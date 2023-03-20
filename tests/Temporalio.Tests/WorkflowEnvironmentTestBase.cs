namespace Temporalio.Tests;

using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Xunit;
using Xunit.Abstractions;

[Collection("Environment")]
public abstract class WorkflowEnvironmentTestBase : TestBase
{
    protected static readonly SearchAttributeKey<bool> AttrBool =
        SearchAttributeKey.CreateBool("DotNetTemporalTestBool");

    protected static readonly SearchAttributeKey<DateTimeOffset> AttrDateTime =
        SearchAttributeKey.CreateDateTimeOffset("DotNetTemporalTestDateTime");

    protected static readonly SearchAttributeKey<double> AttrDouble =
        SearchAttributeKey.CreateDouble("DotNetTemporalTestDouble");

    protected static readonly SearchAttributeKey<string> AttrKeyword =
        SearchAttributeKey.CreateKeyword("DotNetTemporalTestKeyword");

    protected static readonly SearchAttributeKey<IReadOnlyCollection<string>> AttrKeywordList =
        SearchAttributeKey.CreateKeywordList("DotNetTemporalTestKeywordList");

    protected static readonly SearchAttributeKey<long> AttrLong =
        SearchAttributeKey.CreateLong("DotNetTemporalTestLong");

    protected static readonly SearchAttributeKey<string> AttrText =
        SearchAttributeKey.CreateText("DotNetTemporalTestText");

    protected WorkflowEnvironmentTestBase(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output)
    {
        Env = env;
        // We need to update the client logger with our factory
        var newOptions = (TemporalClientOptions)env.Client.Options.Clone();
        newOptions.LoggerFactory = LoggerFactory;
        Client = new TemporalClient(env.Client.Connection, newOptions);
    }

    protected WorkflowEnvironment Env { get; private init; }

    protected ITemporalClient Client { get; private init; }

    protected async Task EnsureSearchAttributesPresentAsync()
    {
        // Only add search attributes if not present
        var resp = await Client.Connection.WorkflowService.GetSearchAttributesAsync(new());
        if (resp.Keys.ContainsKey(AttrBool.Name))
        {
            return;
        }
        await Client.Connection.OperatorService.AddSearchAttributesAsync(new()
        {
            SearchAttributes =
            {
                new Dictionary<string, IndexedValueType>
                {
                    [AttrBool.Name] = AttrBool.ValueType,
                    [AttrDateTime.Name] = AttrDateTime.ValueType,
                    [AttrDouble.Name] = AttrDouble.ValueType,
                    [AttrKeyword.Name] = AttrKeyword.ValueType,
                    // TODO(cretz): Fix after Temporal dev server upgraded
                    // [AttrKeywordList.Name] = AttrKeywordList.ValueType,
                    [AttrLong.Name] = AttrLong.ValueType,
                    [AttrText.Name] = AttrText.ValueType,
                },
            },
        });
        resp = await Client.Connection.WorkflowService.GetSearchAttributesAsync(new());
        Assert.Contains(AttrBool.Name, resp.Keys.Keys);
    }
}
