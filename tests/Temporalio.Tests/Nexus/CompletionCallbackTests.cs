namespace Temporalio.Tests.Nexus;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using NexusRpc;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Converters;
using Temporalio.Nexus;
using Temporalio.Nexus.Notifications;
using Temporalio.Tests.Converters;
using Xunit;

public class CompletionCallbackTests
{
    [Theory]
    [InlineData(null, "op", "queue", "service")]
    [InlineData(" ", "op", "queue", "service")]
    [InlineData("svc", null, "queue", "operation")]
    [InlineData("svc", " ", "queue", "operation")]
    [InlineData("svc", "op", null, "taskQueue")]
    [InlineData("svc", "op", " ", "taskQueue")]
    public void Create_RejectsMissingNames(
        string? service, string? operation, string? taskQueue, string parameter)
    {
        Assert.Equal(
            parameter,
            Assert.Throws<ArgumentException>(
                () => CompletionCallback.Create(service!, operation!, taskQueue!, null)).ParamName);
    }

    [Fact]
    public void Create_TypedResolvesAliasedOperationWithoutInvokingIt()
    {
        var context = new CallbackContext("source");
        CompletionCallback<string> callback = CompletionCallback.Create<IBaseService, string, CallbackContext>(
            (service, request) => service.Completed(request), "callback-queue", context);

        Assert.Equal("aliased-service", callback.Service);
        Assert.Equal("aliased-operation", callback.Operation);
        Assert.Equal("callback-queue", callback.TaskQueue);
        Assert.Same(context, callback.SourceContext);
    }

    [Fact]
    public void Create_RejectsInheritedOperationAbsentFromSelectedService()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CompletionCallback.Create<IChildService, string, CallbackContext>(
                (service, request) => service.Completed(request), "queue", new CallbackContext("source")));

        Assert.Equal("operationCall", exception.ParamName);
        Assert.Contains("registered Nexus completion operation", exception.Message);
    }

    [Fact]
    public void Create_RejectsInheritedOperationWithDifferentServiceAlias()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CompletionCallback.Create<IDifferentAliasService, string, CallbackContext>(
                (service, request) => service.Completed(request), "queue", new CallbackContext("source")));

        Assert.Equal("operationCall", exception.ParamName);
        Assert.Contains("registered Nexus completion operation", exception.Message);
    }

    [Fact]
    public void Create_RejectsNonSymbolicArgument()
    {
        Expression<Func<IChildService, OnCompleteRequest<string, CallbackContext>, OnCompleteResponse>>
            expression = (service, request) => service.Completed(
                new OnCompleteRequest<string, CallbackContext>(
                    new OnCompleteRequestResult<string>.Success("result"), new CallbackContext("other")));

        Assert.Equal(
            "operationCall",
            Assert.Throws<ArgumentException>(
                () => CompletionCallback.Create(expression, "queue", new CallbackContext("source"))).ParamName);
    }

    [Fact]
    public void Create_RejectsUnregisteredOperation()
    {
        Assert.Throws<ArgumentException>(() =>
            CompletionCallback.Create<IChildService, string, CallbackContext>(
                (service, request) => service.Unregistered(request), "queue", new CallbackContext("source")));
    }

    [Fact]
    public void Create_RejectsUnregisteredService()
    {
        Assert.Throws<ArgumentException>(() =>
            CompletionCallback.Create<IUnregisteredService, string, CallbackContext>(
                (service, request) => service.Completed(request), "queue", new CallbackContext("source")));
    }

    [Fact]
    public void Create_RejectsNonCompletionOutput()
    {
        Assert.Throws<ArgumentException>(() =>
            CompletionCallback.Create<IChildService, string, CallbackContext>(
                (service, request) => (OnCompleteResponse)service.WrongResponse(request),
                "queue",
                new CallbackContext("source")));
    }

    [Fact]
    public void Create_RejectsCompatibleButNotExactInput()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CompletionCallback.Create<IChildService, string, CallbackContext>(
                (service, request) => service.BroadInput(request), "queue", new CallbackContext("source")));

        Assert.Contains("exact request and response types", exception.Message);
    }

    [Fact]
    public void Create_RejectsCompatibleButNotExactOutput()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CompletionCallback.Create<IChildService, string, CallbackContext>(
                (service, request) => service.DerivedOutput(request), "queue", new CallbackContext("source")));

        Assert.Contains("exact request and response types", exception.Message);
    }

    [Fact]
    public void Create_TypedAcceptsRawOutput()
    {
        CompletionCallback<RawValue> callback =
            CompletionCallback.Create<IRawOutputService, RawValue, CallbackContext>(
                (service, request) => service.CompletedRaw(request), "queue", new CallbackContext("source"));

        Assert.Equal("RawOutputService", callback.Service);
        Assert.Equal(nameof(IRawOutputService.CompletedRaw), callback.Operation);
    }

    [Fact]
    public void Collection_ReusableDescriptionsPreserveIdentityAcrossCollections()
    {
        var context = new CallbackContext("source");
        CompletionCallback<string> typed = CompletionCallback.Create<IBaseService, string, CallbackContext>(
            (service, request) => service.Completed(request), "typed-queue", context);
        CompletionCallback<RawValue> named = CompletionCallback.Create(
            "named-service", "named-operation", "named-queue", context);
        CompletionCallback<RawValue> raw = CompletionCallback.Create<IRawOutputService, RawValue, CallbackContext>(
            (service, request) => service.CompletedRaw(request), "raw-queue", context);
        var first = new CompletionCallbackCollection<string>.Builder
        {
            typed,
            named,
            raw,
            { "inline-service", "inline-operation", "inline-queue", context },
        }.Build();
        var second = new CompletionCallbackCollection<string>.Builder { typed, raw, named }.Build();

        Assert.Equal(
            new[] { "aliased-operation", "named-operation", "CompletedRaw", "inline-operation" },
            first.Select(callback => callback.Operation));
        Assert.Same(typed, first.First());
        Assert.Same(named, first.ElementAt(1));
        Assert.Same(raw, first.ElementAt(2));
        Assert.Same(typed, second.First());
        Assert.Same(raw, second.ElementAt(1));
        Assert.Same(named, second.Last());
        Assert.Same(context, named.SourceContext);
        Assert.Same(context, raw.SourceContext);
    }

    [Fact]
    public void Collection_ReusableRawDescriptionsAcceptAllResultTypes()
    {
        var context = new CallbackContext("source");
        CompletionCallback<RawValue> concrete = CompletionCallback.Create<IRawOutputService, RawValue, CallbackContext>(
            (service, request) => service.CompletedRaw(request), "queue", context);
        var references = new CompletionCallbackCollection<string>.Builder { concrete }.Build();
        var values = new CompletionCallbackCollection<int>.Builder { concrete }.Build();
        var concreteRaw = new CompletionCallbackCollection<RawValue>.Builder { concrete }.Build();
        var collections = new IReadOnlyCollection<CompletionCallback>[] { references, values, concreteRaw };

        foreach (var callbacks in collections)
        {
            Assert.Same(concrete, Assert.Single(callbacks));
        }
    }

    [Fact]
    public void Collection_NullReusableDescriptionsAreRejected()
    {
        var callbacks = new CompletionCallbackCollection<int>.Builder();

        Assert.Throws<ArgumentNullException>(() => callbacks.Add((CompletionCallback<int>)null!));
        Assert.Throws<ArgumentNullException>(() => callbacks.Add((CompletionCallback<RawValue>)null!));
        Assert.Empty(callbacks);
    }

    [Fact]
    public void Collection_RawCallbacksAcceptReferenceAndValueTypes()
    {
        var context = new CallbackContext("source");
        var references = new CompletionCallbackCollection<string>.Builder();
        var values = new CompletionCallbackCollection<int>.Builder();
        Expression<Func<IRawOutputService, OnCompleteRequest<RawValue, CallbackContext>, OnCompleteResponse>>
            call = (service, request) => service.CompletedRaw(request);
        references.Add(call, "queue", context);
        values.Add(call, "queue", context);

        references.Add(
            (IBaseService service, OnCompleteRequest<string, CallbackContext> request) => service.Completed(request),
            "queue",
            context);
        var options = new NexusOperationOptions<string>("source-id")
        {
            CompletionCallbacks = references.Build(),
        };
        Assert.Equal(2, options.CompletionCallbacks.Count);
        Assert.Equal(references.First().Operation, Assert.Single(values).Operation);
        Assert.Same(context, references.First().SourceContext);
        Assert.Same(context, Assert.Single(values).SourceContext);
    }

    [Fact]
    public void Collection_NullOperationExpression_IsRejected()
    {
        var callbacks = new CompletionCallbackCollection<int>.Builder();
        Assert.Throws<ArgumentNullException>(() => callbacks.Add(
            (Expression<Func<IChildService, OnCompleteRequest<int, CallbackContext>, OnCompleteResponse>>)null!,
            "queue",
            new CallbackContext("source")));
        Assert.Throws<ArgumentNullException>(() => callbacks.Add(
            (Expression<Func<IRawOutputService, OnCompleteRequest<RawValue, CallbackContext>, OnCompleteResponse>>)null!,
            "queue",
            new CallbackContext("source")));
        Assert.Empty(callbacks);
    }

    [Fact]
    public void Collection_RawOutputAndExactInterfaceOutputRemainUsable()
    {
        var context = new CallbackContext("source");
        Expression<Func<IRawOutputService, OnCompleteRequest<RawValue, CallbackContext>, OnCompleteResponse>>
            raw = (service, request) => service.CompletedRaw(request);
        Expression<Func<IRawOutputService, OnCompleteRequest<IRawValue, CallbackContext>, OnCompleteResponse>>
            rawInterface = (service, request) => service.Completed(request);

        var concreteCollection = new CompletionCallbackCollection<RawValue>.Builder
        {
            { raw, "queue", context },
        }.Build();
        var interfaceCollection = new CompletionCallbackCollection<IRawValue>.Builder
        {
            { raw, "queue", context },
            { rawInterface, "queue", context },
        }.Build();

        Assert.Equal("CompletedRaw", Assert.Single(concreteCollection).Operation);
        Assert.Equal(new[] { "CompletedRaw", "Completed" }, interfaceCollection.Select(item => item.Operation));
    }

    [Fact]
    public void Collection_DoesNotExposeUntypedInsertion()
    {
        var callbacks = new CompletionCallbackCollection<int>();
        Assert.False(typeof(ICollection<CompletionCallback>).IsAssignableFrom(callbacks.GetType()));
        Assert.DoesNotContain(callbacks.GetType().GetMethods(), method => method.Name == "Add");
        var descriptionAddTypes = typeof(CompletionCallbackCollection<int>.Builder).GetMethods()
            .Where(method => method.Name == "Add" && method.GetParameters().Length == 1)
            .Select(method => method.GetParameters()[0].ParameterType)
            .ToArray();
        Assert.Equal(2, descriptionAddTypes.Length);
        Assert.Contains(typeof(CompletionCallback<int>), descriptionAddTypes);
        Assert.DoesNotContain(typeof(CompletionCallback<IRawValue>), descriptionAddTypes);
        Assert.Contains(typeof(CompletionCallback<RawValue>), descriptionAddTypes);
        Assert.DoesNotContain(typeof(CompletionCallback), descriptionAddTypes);
        Assert.DoesNotContain(typeof(CompletionCallback<string>), descriptionAddTypes);
    }

    [Fact]
    public void Builder_BuildSnapshotsAreSafeToShareAcrossOptions()
    {
        var builder = new CompletionCallbackCollection<int>.Builder();
        var empty = builder.Build();
        var callbacks = builder.Add("service", "first", "queue", null).Build();
        var options = new NexusOperationOptions<int>("source-id") { CompletionCallbacks = callbacks };
        var clone = (NexusOperationOptions<int>)options.Clone();
        var request = options.ToUntyped();

        var updated = builder.Add("service", "second", "queue", null).Build();
        builder.Add("service", "third", "queue", null);

        Assert.Empty(empty);
        Assert.Same(callbacks, clone.CompletionCallbacks);
        Assert.Same(callbacks, request.CompletionCallbacks);
        Assert.Equal("first", Assert.Single(options.CompletionCallbacks!).Operation);
        Assert.Equal("first", Assert.Single(clone.CompletionCallbacks!).Operation);
        Assert.Equal("first", Assert.Single(request.CompletionCallbacks!).Operation);
        Assert.Equal(new[] { "first", "second" }, updated.Select(callback => callback.Operation));
        Assert.Equal(3, builder.Count());
        Assert.Equal(
            callbacks,
            ((System.Collections.IEnumerable)callbacks).Cast<CompletionCallback>());
    }

    [Fact]
    public void Builder_AddOverloadsSupportChaining()
    {
        var context = new CallbackContext("source");
        var typed = CompletionCallback.Create<IBaseService, string, CallbackContext>(
            (service, request) => service.Completed(request), "queue", context);
        var raw = CompletionCallback.Create("service", "raw", "queue", context);
        var callbacks = new CompletionCallbackCollection<string>.Builder()
            .Add(typed)
            .Add(raw)
            .Add(
                (IBaseService service, OnCompleteRequest<string, CallbackContext> request) => service.Completed(request),
                "queue",
                context)
            .Add(
                (IRawOutputService service, OnCompleteRequest<RawValue, CallbackContext> request) => service.CompletedRaw(request),
                "queue",
                context)
            .Add("service", "named", "queue", context)
            .Build();

        Assert.Equal(
            new[] { "aliased-operation", "raw", "aliased-operation", "CompletedRaw", "named" },
            callbacks.Select(callback => callback.Operation));
        Assert.Same(typed, callbacks.First());
        Assert.Same(raw, callbacks.ElementAt(1));
    }

    [Fact]
    public void TypedOptions_CloneAndRequestConversion_PreserveSettings()
    {
        var callbacks = new CompletionCallbackCollection<int>.Builder
        {
            { "service", "operation", "queue", null },
        }.Build();
        var callback = Assert.Single(callbacks);
        var options = new NexusOperationOptions<int>("source-id")
        {
            ScheduleToCloseTimeout = TimeSpan.FromSeconds(10),
            Summary = "source",
            SearchAttributes = SearchAttributeCollection.Empty,
            IdReusePolicy = NexusOperationIdReusePolicy.RejectDuplicate,
            IdConflictPolicy = NexusOperationIdConflictPolicy.UseExisting,
            Rpc = new() { Timeout = TimeSpan.FromSeconds(3) },
            CompletionCallbacks = callbacks,
        };
        var copy = (NexusOperationOptions<int>)options.Clone();
        var request = options.ToUntyped();

        foreach (var converted in new NexusOperationOptionsBase[] { request, copy })
        {
            Assert.Equal(options.Id, converted.Id);
            Assert.Equal(options.ScheduleToCloseTimeout, converted.ScheduleToCloseTimeout);
            Assert.Equal(options.Summary, converted.Summary);
            Assert.Same(options.SearchAttributes, converted.SearchAttributes);
            Assert.Equal(options.IdReusePolicy, converted.IdReusePolicy);
            Assert.Equal(options.IdConflictPolicy, converted.IdConflictPolicy);
            Assert.Equal(options.Rpc.Timeout, converted.Rpc!.Timeout);
            Assert.NotSame(options.Rpc, converted.Rpc);
        }
        Assert.Same(options.CompletionCallbacks, copy.CompletionCallbacks);
        Assert.Same(callback, Assert.Single(request.CompletionCallbacks!));
        copy.Id = "copy-id";
        Assert.Equal("source-id", options.Id);
    }

    [Fact]
    public void TypedOptions_DoNotEraseResultTypeThroughInheritance()
    {
        Assert.False(typeof(NexusOperationOptions).IsAssignableFrom(typeof(NexusOperationOptions<string>)));
        Assert.Equal(typeof(NexusOperationOptionsBase), typeof(NexusOperationOptions).BaseType);
        Assert.Equal(typeof(NexusOperationOptionsBase), typeof(NexusOperationOptions<string>).BaseType);
    }

    [Fact]
    public void UntypedOptions_Clone_PreservesRuntimeTypeAndCopiesRpcOptions()
    {
        var options = new NexusOperationOptions("source-id")
        {
            Rpc = new() { Timeout = TimeSpan.FromSeconds(3) },
            CompletionCallbacks = new CompletionCallbackCollection<int>.Builder
            {
                { "service", "operation", "queue", null },
            }.Build(),
        };

        var copy = Assert.IsType<NexusOperationOptions>(options.Clone());

        Assert.Equal(options.Id, copy.Id);
        Assert.Same(options.CompletionCallbacks, copy.CompletionCallbacks);
        Assert.Equal(options.Rpc.Timeout, copy.Rpc!.Timeout);
        Assert.NotSame(options.Rpc, copy.Rpc);
        copy.Id = "copy-id";
        Assert.Equal("source-id", options.Id);
    }

    [Fact]
    public void Concat_PreservesOrderAndDoesNotChangeSources()
    {
        var commonBuilder = new CompletionCallbackCollection<string>.Builder
        {
            { "service", "common", "queue", "shared" },
        };
        var common = commonBuilder.Build();
        var specific = new CompletionCallbackCollection<string>.Builder
        {
            { "service", "specific", "queue", "request" },
        }.Build();
        var combined = common.Concat(specific);

        Assert.Equal(new[] { "common", "specific" }, combined.Select(callback => callback.Operation));
        Assert.Same(Assert.Single(common), combined.First());
        Assert.Same(Assert.Single(specific), combined.Last());

        commonBuilder.Add("service", "later", "queue", null);
        Assert.Single(common);
        Assert.Single(specific);
        Assert.Equal(new[] { "common", "specific" }, combined.Select(callback => callback.Operation));
    }

    [Fact]
    public void Concat_RawCollectionsWorkForReferenceAndValueTypes()
    {
        var typed = new CompletionCallbackCollection<string>.Builder
        {
            { "service", "typed", "queue", null },
        }.Build();
        var raw = new CompletionCallbackCollection<RawValue>.Builder
        {
            { "service", "raw", "queue", null },
        }.Build();
        var references = typed.Concat(raw);
        var values = new CompletionCallbackCollection<int>().Concat(raw);
        Assert.Single(raw);

        Assert.Equal(new[] { "typed", "raw" }, references.Select(callback => callback.Operation));
        Assert.Equal("raw", Assert.Single(values).Operation);
        Assert.Single(typed);
    }

    [Fact]
    public void Concat_RawResultCollectionsRemainUsable()
    {
        var concrete = new CompletionCallbackCollection<RawValue>.Builder
        {
            { "service", "concrete", "queue", null },
        }.Build();
        var rawInterface = new CompletionCallbackCollection<IRawValue>.Builder
        {
            { "service", "interface", "queue", null },
        }.Build();

        Assert.Equal(2, concrete.Concat(concrete).Count);
        Assert.Equal(2, rawInterface.Concat(concrete).Count);
        Assert.Equal(2, rawInterface.Concat(rawInterface).Count);
    }

    [Fact]
    public void Concat_NullCollection_IsRejected()
    {
        var callbacks = new CompletionCallbackCollection<int>();
        Assert.Throws<ArgumentNullException>(() => callbacks.Concat((CompletionCallbackCollection<int>)null!));
        Assert.Throws<ArgumentNullException>(() => callbacks.Concat((CompletionCallbackCollection<RawValue>)null!));
    }

    [Fact]
    public void CallbackDescriptions_DoNotExposeConstruction()
    {
        Assert.True(typeof(CompletionCallback).IsAbstract);
        Assert.True(typeof(CompletionCallback<RawValue>).IsSealed);
        Assert.Equal(typeof(CompletionCallback), typeof(CompletionCallback<RawValue>).BaseType);
        Assert.Empty(typeof(CompletionCallback).GetConstructors());
        Assert.Empty(typeof(CompletionCallback<RawValue>).GetConstructors());
        var factories = typeof(CompletionCallback).GetMethods()
            .Where(method => method.Name == "Create")
            .ToArray();
        Assert.Equal(2, factories.Length);
        Assert.Equal(
            typeof(CompletionCallback<RawValue>),
            Assert.Single(factories, method => !method.IsGenericMethodDefinition).ReturnType);
        Assert.Equal(
            typeof(CompletionCallback<>),
            Assert.Single(factories, method => method.IsGenericMethodDefinition).ReturnType.GetGenericTypeDefinition());
    }

    [Fact]
    public void CompletionNotification_IncompatibleOutput_FailsDuringDecoding()
    {
        var converter = new SystemNexusPayloadConverter(
            DataConverter.Default.PayloadConverter,
            DataConverter.Default.FailureConverter);
        var request = new OnCompleteRequest<int, CallbackContext>(
            new OnCompleteRequestResult<int>.Success(42),
            new CallbackContext("source"));

        var payload = converter.ToPayload(request);

        Assert.Throws<JsonException>(() =>
            converter.ToValue(payload, typeof(OnCompleteRequest<string, CallbackContext>)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ToProtoAsync_EncodesContextIncludingNull(bool isNull)
    {
        var context = isNull ? null : new CallbackContext("source");
        var callback = CompletionCallback.Create("svc", "op", "queue", context);
        var codec = new Base64PayloadCodec();
        var converter = DataConverter.Default with { PayloadCodec = codec };

        var proto = await callback.ToProtoAsync(converter);

        Assert.Equal("svc", proto.NexusHandler.Service);
        Assert.Equal("op", proto.NexusHandler.Operation);
        Assert.Equal("queue", proto.NexusHandler.TaskQueueName);
        Assert.Equal(
            Base64PayloadCodec.EncodingName,
            proto.NexusHandler.SourceContext.Metadata["encoding"].ToStringUtf8());
        var decoded = (await codec.DecodeAsync(new[] { proto.NexusHandler.SourceContext })).Single();
        Assert.Equal(
            context,
            converter.PayloadConverter.ToValue(decoded, typeof(CallbackContext)));
    }

    public sealed record CallbackContext(string Value);

    public sealed record DerivedResponse : OnCompleteResponse;

    [NexusService("aliased-service")]
    public interface IBaseService
    {
        [NexusOperation("aliased-operation")]
        OnCompleteResponse Completed(OnCompleteRequest<string, CallbackContext> request);
    }

    [NexusService("aliased-service")]
    public interface IChildService : IBaseService
    {
        OnCompleteResponse Unregistered(OnCompleteRequest<string, CallbackContext> request);

        [NexusOperation]
        object WrongResponse(OnCompleteRequest<string, CallbackContext> request);

        [NexusOperation]
        OnCompleteResponse BroadInput(object request);

        [NexusOperation]
        DerivedResponse DerivedOutput(OnCompleteRequest<string, CallbackContext> request);
    }

    [NexusService("different-service")]
    public interface IDifferentAliasService : IBaseService
    {
        [NexusOperation]
        string Own(string input);
    }

    public interface IUnregisteredService
    {
        OnCompleteResponse Completed(OnCompleteRequest<string, CallbackContext> request);
    }

    [NexusService]
    public interface IRawOutputService
    {
        [NexusOperation]
        OnCompleteResponse Completed(OnCompleteRequest<IRawValue, CallbackContext> request);

        [NexusOperation]
        OnCompleteResponse CompletedRaw(OnCompleteRequest<RawValue, CallbackContext> request);
    }
}
