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
        CompletionCallback<IRawValue> callback =
            CompletionCallback.Create<IRawOutputService, IRawValue, CallbackContext>(
                (service, request) => service.Completed(request), "queue", new CallbackContext("source"));

        Assert.Equal("RawOutputService", callback.Service);
        Assert.Equal(nameof(IRawOutputService.Completed), callback.Operation);
    }

    [Fact]
    public void Collection_ReusableDescriptionsPreserveIdentityAcrossCollections()
    {
        var context = new CallbackContext("source");
        CompletionCallback<string> typed = CompletionCallback.Create<IBaseService, string, CallbackContext>(
            (service, request) => service.Completed(request), "typed-queue", context);
        CompletionCallback<RawValue> named = CompletionCallback.Create(
            "named-service", "named-operation", "named-queue", context);
        CompletionCallback<IRawValue> raw = CompletionCallback.Create<IRawOutputService, IRawValue, CallbackContext>(
            (service, request) => service.Completed(request), "raw-queue", context);
        var first = new CompletionCallbackCollection<string>
        {
            typed,
            named,
            raw,
            { "inline-service", "inline-operation", "inline-queue", context },
        };
        var second = new CompletionCallbackCollection<string> { typed, raw, named };

        Assert.Equal(
            new[] { "aliased-operation", "named-operation", "Completed", "inline-operation" },
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
        CompletionCallback<IRawValue> rawInterface =
            CompletionCallback.Create<IRawOutputService, IRawValue, CallbackContext>(
                (service, request) => service.Completed(request), "queue", context);
        var references = new CompletionCallbackCollection<string> { concrete, rawInterface };
        var values = new CompletionCallbackCollection<int> { concrete, rawInterface };
        var concreteRaw = new CompletionCallbackCollection<RawValue> { concrete, rawInterface };
        var interfaceRaw = new CompletionCallbackCollection<IRawValue> { concrete, rawInterface };
        var collections = new IReadOnlyCollection<CompletionCallback>[] { references, values, concreteRaw, interfaceRaw };

        foreach (var callbacks in collections)
        {
            Assert.Equal(2, callbacks.Count);
            Assert.Same(concrete, callbacks.First());
            Assert.Same(rawInterface, callbacks.Last());
        }
    }

    [Fact]
    public void Collection_NullReusableDescriptionsAreRejected()
    {
        var callbacks = new CompletionCallbackCollection<int>();

        Assert.Throws<ArgumentNullException>(() => callbacks.Add((CompletionCallback<int>)null!));
        Assert.Throws<ArgumentNullException>(() => callbacks.Add((CompletionCallback<IRawValue>)null!));
        Assert.Throws<ArgumentNullException>(() => callbacks.Add((CompletionCallback<RawValue>)null!));
        Assert.Empty(callbacks);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Collection_RawCallbacksAcceptReferenceAndValueTypes(bool concreteRawValue)
    {
        var context = new CallbackContext("source");
        var references = new CompletionCallbackCollection<string>();
        var values = new CompletionCallbackCollection<int>();
        if (concreteRawValue)
        {
            Expression<Func<IRawOutputService, OnCompleteRequest<RawValue, CallbackContext>, OnCompleteResponse>>
                call = (service, request) => service.CompletedRaw(request);
            references.Add(call, "queue", context);
            values.Add(call, "queue", context);
        }
        else
        {
            Expression<Func<IRawOutputService, OnCompleteRequest<IRawValue, CallbackContext>, OnCompleteResponse>>
                call = (service, request) => service.Completed(request);
            references.Add(call, "queue", context);
            values.Add(call, "queue", context);
        }

        var options = new NexusOperationOptions<string>("source-id")
        {
            CompletionCallbacks = references,
        };
        options.CompletionCallbacks.Add(
            (IBaseService service, OnCompleteRequest<string, CallbackContext> request) => service.Completed(request),
            "queue",
            context);
        Assert.Equal(2, options.CompletionCallbacks.Count);
        Assert.Equal(references.First().Operation, Assert.Single(values).Operation);
        Assert.Same(context, references.First().SourceContext);
        Assert.Same(context, Assert.Single(values).SourceContext);
    }

    [Fact]
    public void Collection_NullOperationExpression_IsRejected()
    {
        var callbacks = new CompletionCallbackCollection<int>();
        Assert.Throws<ArgumentNullException>(() => callbacks.Add(
            (Expression<Func<IChildService, OnCompleteRequest<int, CallbackContext>, OnCompleteResponse>>)null!,
            "queue",
            new CallbackContext("source")));
        Assert.Throws<ArgumentNullException>(() => callbacks.Add(
            (Expression<Func<IRawOutputService, OnCompleteRequest<RawValue, CallbackContext>, OnCompleteResponse>>)null!,
            "queue",
            new CallbackContext("source")));
        Assert.Throws<ArgumentNullException>(() => callbacks.Add(
            (Expression<Func<IRawOutputService, OnCompleteRequest<IRawValue, CallbackContext>, OnCompleteResponse>>)null!,
            "queue",
            new CallbackContext("source")));
        Assert.Empty(callbacks);
    }

    [Fact]
    public void Collection_RawOutputAcceptsBothRawTypes()
    {
        var context = new CallbackContext("source");
        Expression<Func<IRawOutputService, OnCompleteRequest<RawValue, CallbackContext>, OnCompleteResponse>>
            raw = (service, request) => service.CompletedRaw(request);
        Expression<Func<IRawOutputService, OnCompleteRequest<IRawValue, CallbackContext>, OnCompleteResponse>>
            rawInterface = (service, request) => service.Completed(request);

        var concreteCollection = new CompletionCallbackCollection<RawValue>
        {
            { raw, "queue", context },
            { rawInterface, "queue", context },
        };
        var interfaceCollection = new CompletionCallbackCollection<IRawValue>
        {
            { raw, "queue", context },
            { rawInterface, "queue", context },
        };

        Assert.Equal(new[] { "CompletedRaw", "Completed" }, concreteCollection.Select(item => item.Operation));
        Assert.Equal(new[] { "CompletedRaw", "Completed" }, interfaceCollection.Select(item => item.Operation));
    }

    [Fact]
    public void Collection_DoesNotExposeUntypedInsertion()
    {
        var callbacks = new CompletionCallbackCollection<int>();
        Assert.False(typeof(ICollection<CompletionCallback>).IsAssignableFrom(callbacks.GetType()));
        var descriptionAddTypes = callbacks.GetType().GetMethods()
            .Where(method => method.Name == "Add" && method.GetParameters().Length == 1)
            .Select(method => method.GetParameters()[0].ParameterType)
            .ToArray();
        Assert.Equal(3, descriptionAddTypes.Length);
        Assert.Contains(typeof(CompletionCallback<int>), descriptionAddTypes);
        Assert.Contains(typeof(CompletionCallback<IRawValue>), descriptionAddTypes);
        Assert.Contains(typeof(CompletionCallback<RawValue>), descriptionAddTypes);
        Assert.DoesNotContain(typeof(CompletionCallback), descriptionAddTypes);
        Assert.DoesNotContain(typeof(CompletionCallback<string>), descriptionAddTypes);
    }

    [Fact]
    public void TypedOptions_CloneAndRequestConversion_PreserveSettings()
    {
        var callbacks = new CompletionCallbackCollection<int> { { "service", "operation", "queue", null } };
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
            CompletionCallbacks = new CompletionCallbackCollection<int>
            {
                { "service", "operation", "queue", null },
            },
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
        var common = new CompletionCallbackCollection<string> { { "service", "common", "queue", "shared" } };
        var specific = new CompletionCallbackCollection<string> { { "service", "specific", "queue", "request" } };
        var combined = common.Concat(specific);

        Assert.Equal(new[] { "common", "specific" }, combined.Select(callback => callback.Operation));
        Assert.Same(Assert.Single(common), combined.First());
        Assert.Same(Assert.Single(specific), combined.Last());

        combined.Add("service", "extra", "queue", null);
        Assert.Single(common);
        Assert.Single(specific);
        common.Add("service", "later", "queue", null);
        Assert.Equal(new[] { "common", "specific", "extra" }, combined.Select(callback => callback.Operation));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Concat_RawCollectionsWorkForReferenceAndValueTypes(bool concreteRawValue)
    {
        var typed = new CompletionCallbackCollection<string> { { "service", "typed", "queue", null } };
        CompletionCallbackCollection<string> references;
        CompletionCallbackCollection<int> values;
        if (concreteRawValue)
        {
            var raw = new CompletionCallbackCollection<RawValue> { { "service", "raw", "queue", null } };
            references = typed.Concat(raw);
            values = new CompletionCallbackCollection<int>().Concat(raw);
            Assert.Single(raw);
        }
        else
        {
            var raw = new CompletionCallbackCollection<IRawValue> { { "service", "raw", "queue", null } };
            references = typed.Concat(raw);
            values = new CompletionCallbackCollection<int>().Concat(raw);
            Assert.Single(raw);
        }

        Assert.Equal(new[] { "typed", "raw" }, references.Select(callback => callback.Operation));
        Assert.Equal("raw", Assert.Single(values).Operation);
        Assert.Single(typed);
    }

    [Fact]
    public void Concat_RawResultCollectionsRemainUsable()
    {
        var concrete = new CompletionCallbackCollection<RawValue> { { "service", "concrete", "queue", null } };
        var rawInterface = new CompletionCallbackCollection<IRawValue> { { "service", "interface", "queue", null } };

        Assert.Equal(2, concrete.Concat(concrete).Count);
        Assert.Equal(2, concrete.Concat(rawInterface).Count);
        Assert.Equal(2, rawInterface.Concat(concrete).Count);
        Assert.Equal(2, rawInterface.Concat(rawInterface).Count);
    }

    [Fact]
    public void Concat_NullCollection_IsRejected()
    {
        var callbacks = new CompletionCallbackCollection<int>();
        Assert.Throws<ArgumentNullException>(() => callbacks.Concat((CompletionCallbackCollection<int>)null!));
        Assert.Throws<ArgumentNullException>(() => callbacks.Concat((CompletionCallbackCollection<RawValue>)null!));
        Assert.Throws<ArgumentNullException>(() => callbacks.Concat((CompletionCallbackCollection<IRawValue>)null!));
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
