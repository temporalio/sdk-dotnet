using Temporalio.Exceptions;

namespace Temporalio.Tests;

using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Common;
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

    /// <summary>Check for Worker Versioning feature support.</summary>
    /// <returns>True if the server supports it.</returns>
    protected async Task<bool> ServerSupportsWorkerVersioning()
    {
        var tq = $"tq-test-worker-ver-{Guid.NewGuid()}";
        try
        {
            await Client.UpdateWorkerBuildIdCompatibilityAsync(tq, new BuildIdOp.AddNewDefault("yoyoyo"));
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.PermissionDenied ||
                                     e.Code == RpcException.StatusCode.Unimplemented)
        {
            return false;
        }

        return true;
    }

    protected async Task EnsureSearchAttributesPresentAsync()
    {
        // Only add search attributes if not present
        var resp = await Client.Connection.OperatorService.ListSearchAttributesAsync(
            new() { Namespace = Client.Options.Namespace });
        if (resp.CustomAttributes.ContainsKey(AttrBool.Name))
        {
            return;
        }
        await Client.Connection.OperatorService.AddSearchAttributesAsync(new()
        {
            Namespace = Client.Options.Namespace,
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
        resp = await Client.Connection.OperatorService.ListSearchAttributesAsync(
            new() { Namespace = Client.Options.Namespace });
        Assert.Contains(AttrBool.Name, resp.CustomAttributes.Keys);
    }
}
