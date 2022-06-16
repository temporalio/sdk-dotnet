using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Temporal.Common;
using Temporal.Serialization;
using Temporal.Util;
using SerializedPayloads = Temporal.Api.Common.V1.Payloads;

// <summary>
// This file contains design-phase APIs.
// We will refactor and implement after the Activity Worker design is complete.
// </summary>
namespace Temporal.Worker.Hosting
{
    public static class Api
    {
    }

    public class TemporalActivityWorker : IDisposable
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Task StartAsync()
        {
            throw new NotImplementedException();
        }

        public Task TerminateAsync()
        {
            throw new NotImplementedException();
        }


        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0059:Unnecessary assignment of a value", Justification = "PoC")]
        public TemporalActivityWorker RegisterActivity<TArg, TResult>(string activityTypeName,
                                                                      IActivityImplementationFactory<TArg, TResult> activityFactory)
        {
            // `activityTypeName` may be null;
            Validate.NotNull(activityFactory);

            // ...
            activityTypeName = activityTypeName ?? activityFactory.ActivityTypeName;
            ActivityExecutor<TArg, TResult> activityExecutor = new(activityTypeName, activityFactory);
            // ...

            throw new NotImplementedException("@ToDo");
            //return this;
        }
    }

    public static class TemporalActivityWorkerExtensions
    {
        public static TemporalActivityWorker RegisterActivity<TArg, TResult>(this TemporalActivityWorker worker,
                                                                             IActivityImplementationFactory<TArg, TResult> activityFactory)
        {
            return worker.RegisterActivity(activityFactory.ActivityTypeName, activityFactory);
        }

        #region RegisterActivity takes `Func<.., Task<TResult>>` with 0 inputs

        public static TemporalActivityWorker RegisterActivity<TResult>(this TemporalActivityWorker worker,
                                                                       string activityTypeName,
                                                                       Func<Task<TResult>> activity)
        {
            return worker.RegisterActivity(
                    new BasicActivityImplementationFactory<IPayload.Void, TResult>(
                            activityTypeName,
                            (_, __) => activity()));
        }

        public static TemporalActivityWorker RegisterActivity<TArg, TResult>(this TemporalActivityWorker worker,
                                                                             string activityTypeName,
                                                                             Func<IWorkflowActivityContext, Task<TResult>> activity)
        {
            return worker.RegisterActivity(
                    new BasicActivityImplementationFactory<TArg, TResult>(
                            activityTypeName,
                            (_, ctx) => activity(ctx)));
        }

        #endregion RegisterActivity takes `Func<.., Task<TResult>>` with 0 inputs

        #region RegisterActivity takes `Func<.., Task<TResult>>` with 1 input

        public static TemporalActivityWorker RegisterActivity<TArg, TResult>(this TemporalActivityWorker worker,
                                                                             string activityTypeName,
                                                                             Func<TArg, Task<TResult>> activity)
        {
            return worker.RegisterActivity(
                    new BasicActivityImplementationFactory<TArg, TResult>(
                            activityTypeName,
                            (inp, _) => activity(inp)));
        }

        public static TemporalActivityWorker RegisterActivity<TArg, TResult>(this TemporalActivityWorker worker,
                                                                             string activityTypeName,
                                                                             Func<TArg, IWorkflowActivityContext, Task<TResult>> activity)
        {
            return worker.RegisterActivity(
                    new BasicActivityImplementationFactory<TArg, TResult>(
                            activityTypeName,
                            (inp, ctx) => activity(inp, ctx)));
        }

        #endregion RegisterActivity takes `Func<.., Task<TResult>>` with 1 input

        #region RegisterActivity takes async `Func<.., Task>` with 0 inputs (Task is NOT Task<TResult>)

        public static TemporalActivityWorker RegisterActivity(this TemporalActivityWorker worker,
                                                              string activityTypeName,
                                                              Func<Task> activity)
        {
            return worker.RegisterActivity(
                    new BasicActivityImplementationFactory<IPayload.Void, IPayload.Void>(
                            activityTypeName,
                            async (_, __) =>
                            {
                                await activity();
                                return IPayload.Void.Instance;
                            }));
        }

        public static TemporalActivityWorker RegisterActivity(this TemporalActivityWorker worker,
                                                              string activityTypeName,
                                                              Func<IWorkflowActivityContext, Task> activity)
        {
            return worker.RegisterActivity(
                    new BasicActivityImplementationFactory<IPayload.Void, IPayload.Void>(
                            activityTypeName,
                            async (_, ctx) =>
                            {
                                await activity(ctx);
                                return IPayload.Void.Instance;
                            }));
        }

        #endregion RegisterActivity takes async `Func<.., Task>` with 0 inputs (Task is NOT Task<TResult>)

        #region RegisterActivity takes async `Func<.., Task>` with 1 input (Task is NOT Task<TResult>)

        public static TemporalActivityWorker RegisterActivity<TArg>(this TemporalActivityWorker worker,
                                                                    string activityTypeName,
                                                                    Func<TArg, Task> activity)
        {
            return worker.RegisterActivity(
                    new BasicActivityImplementationFactory<TArg, IPayload.Void>(
                            activityTypeName,
                            async (inp, _) =>
                            {
                                await activity(inp);
                                return IPayload.Void.Instance;
                            }));
        }

        public static TemporalActivityWorker RegisterActivity<TArg>(this TemporalActivityWorker worker,
                                                                    string activityTypeName,
                                                                    Func<TArg, IWorkflowActivityContext, Task> activity)
        {
            return worker.RegisterActivity(
                    new BasicActivityImplementationFactory<TArg, IPayload.Void>(
                            activityTypeName,
                            async (inp, ctx) =>
                            {
                                await activity(inp, ctx);
                                return IPayload.Void.Instance;
                            }));
        }

        #endregion RegisterActivity takes async `Func<.., Task>` with 1 input (Task is NOT Task<TResult>)

        #region RegisterActivity takes `Func<.., TResult>` with 0 inputs (`TResult` must not be Task)

        public static TemporalActivityWorker RegisterActivity<TResult>(this TemporalActivityWorker worker,
                                                                       string activityTypeName,
                                                                       Func<TResult> activity)
        {
            if (Types.ImplementsInterface(typeof(TResult), typeof(IActivityImplementation<,>)))
            {
                throw new ArgumentException($"The specified {nameof(activity)} is a `Func` that takes zero arguments"
                                          + $" and returns an instance of `{typeof(TResult).ToString()}` which implements"
                                          + $" `{typeof(IActivityImplementation<,>).Name}` and is not a data transport type."
                                          + $" You probably intended to invoke the {nameof(RegisterActivity)}(..)-overload"
                                          + $" that takes the argument `Func<IActivityImplementation<TArg, TResult>> activityFactory`"
                                          + $" instead of `Func<TResult> activity`. To invoke the intended overload,"
                                          + $" use a named argument (`activityFactory: ..`) or make sure that the specified Func"
                                          + $" explicitly returns an `IActivityImplementation<,>` instance. For example:"
                                          + $" `worker.RegisterActivity(\"ActivityTypeName\","
                                          + $" () => (IActivityImplementation<TArg, TRes>) (new SayHelloActivity()))`.");
            }

            return worker.RegisterActivity(
                    new BasicActivityImplementationFactory<IPayload.Void, TResult>(
                            activityTypeName,
                            (_, __) =>
                            {
                                TResult r = activity();

                                if (r is Task<TResult> taskRes)
                                {
                                    return taskRes;
                                }

                                return Task.FromResult(r);
                            }));
        }

        public static TemporalActivityWorker RegisterActivity<TResult>(this TemporalActivityWorker worker,
                                                                       string activityTypeName,
                                                                       Func<IWorkflowActivityContext, TResult> activity)
        {
            return worker.RegisterActivity(
                    new BasicActivityImplementationFactory<IPayload.Void, TResult>(
                            activityTypeName,
                            (_, ctx) =>
                            {
                                TResult r = activity(ctx);
                                return Task.FromResult(r);
                            }));
        }

        #endregion RegisterActivity takes `Func<.., TResult>` with 0 inputs (`TResult` must not be Task)

        #region RegisterActivity takes `Func<.., TResult>` with 1 input (`TResult` must not be Task)

        public static TemporalActivityWorker RegisterActivity<TArg, TResult>(this TemporalActivityWorker worker,
                                                                             string activityTypeName,
                                                                             Func<TArg, TResult> activity)
        {
            return worker.RegisterActivity(
                    new BasicActivityImplementationFactory<TArg, TResult>(
                            activityTypeName,
                            (inp, _) =>
                            {
                                TResult r = activity(inp);
                                return Task.FromResult(r);
                            }));
        }

        public static TemporalActivityWorker RegisterActivity<TArg, TResult>(this TemporalActivityWorker worker,
                                                                             string activityTypeName,
                                                                             Func<TArg, IWorkflowActivityContext, TResult> activity)
        {
            return worker.RegisterActivity(
                    new BasicActivityImplementationFactory<TArg, TResult>(
                            activityTypeName,
                            (inp, ctx) =>
                            {
                                TResult r = activity(inp, ctx);
                                return Task.FromResult(r);
                            }));
        }

        #endregion RegisterActivity takes `Func<.., TResult>` with 1 input (`TResult` must not be Task)

        #region RegisterActivity takes `Action<..>` with 0 inputs

        public static TemporalActivityWorker RegisterActivity(this TemporalActivityWorker worker,
                                                              string activityTypeName,
                                                              Action activity)
        {
            return worker.RegisterActivity(
                    new BasicActivityImplementationFactory<IPayload.Void, IPayload.Void>(
                            activityTypeName,
                            (_, __) =>
                            {
                                activity();
                                return IPayload.Void.CompletedTask;
                            }));
        }

        public static TemporalActivityWorker RegisterActivity(this TemporalActivityWorker worker,
                                                              string activityTypeName,
                                                              Action<IWorkflowActivityContext> activity)
        {
            return worker.RegisterActivity(
                    new BasicActivityImplementationFactory<IPayload.Void, IPayload.Void>(
                            activityTypeName,
                            (_, ctx) =>
                            {
                                activity(ctx);
                                return IPayload.Void.CompletedTask;
                            }));
        }

        #endregion RegisterActivity takes `Action<..>` with 0 inputs

        #region RegisterActivity takes `Action<..>` with 1 input

        public static TemporalActivityWorker RegisterActivity<TArg>(this TemporalActivityWorker worker,
                                                                    string activityTypeName,
                                                                    Action<TArg> activity)
        {
            return worker.RegisterActivity(
                    new BasicActivityImplementationFactory<TArg, IPayload.Void>(
                            activityTypeName,
                            (inp, _) =>
                            {
                                activity(inp);
                                return IPayload.Void.CompletedTask;
                            }));
        }

        public static TemporalActivityWorker RegisterActivity<TArg>(this TemporalActivityWorker worker,
                                                                    string activityTypeName,
                                                                    Action<TArg, IWorkflowActivityContext> activity)
        {
            return worker.RegisterActivity(
                    new BasicActivityImplementationFactory<TArg, IPayload.Void>(
                            activityTypeName,
                            (inp, ctx) =>
                            {
                                activity(inp, ctx);
                                return IPayload.Void.CompletedTask;
                            }));
        }

        #endregion HostActivity takes `Action<..>` with 1 input

    }

    public record ActivityInvocationPipelineItemFactoryArguments(string ActivityTypeName,
                                                                 object ActivityImplementationFactory)
    {
    }

    public interface ITemporalActivityInterceptor
    {
    }

    public record TemporalActivityWorkerConfiguration(
            string Namespace,                             // namespace
            string ActivityTaskQueue,                     // task_queue
            int ConcurrentActivitiesMaxCount,             // max_outstanding_activities (def=100),
            int ConcurrentActivityPollRequestsMacCount,   // max_concurrent_at_polls (def=5)
            TimeSpan HeartbeatThrottleIntervalMax,        // max_heartbeat_throttle_interval (def=60sec)
            TimeSpan HeartbeatThrottleIntervalDefault,    // default_heartbeat_throttle_interval (def=30sec)
            double ActivityTasksEnqueuedPerSecondMax,     // max_task_queue_activities_per_second (def=??)
            double ActivityTasksDequeuedPerSecondMax,     // max_worker_activities_per_second (def=??)
            Func<ActivityInvocationPipelineItemFactoryArguments, IPayloadConverter> PayloadConverterFactory,
            Func<ActivityInvocationPipelineItemFactoryArguments, IPayloadCodec> PayloadCodecFactory,
            Action<ActivityInvocationPipelineItemFactoryArguments, IList<ITemporalActivityInterceptor>> ActivityInterceptorFactory)
    {
    }

    /// <summary>
    /// @ToDo: need to move to "Common" 
    /// </summary>
    /// <param name="InitialInterval">Interval of the first retry.
    /// If retryBackoffCoefficient is 1.0 then it is used for all retries.</param>
    /// <param name="BackoffCoefficient">Coefficient used to calculate the next retry interval. The next
    /// retry interval is previous interval multiplied by the coefficient. Must be 1 or larger.</param>
    /// <param name="MaximumInterval">Maximum interval between retries. Exponential backoff leads to interval
    /// increase. This value is the cap of the increase. Default is 100x of the initial interval.</param>
    /// <param name="MaximumAttempts">Maximum number of attempts. When exceeded the retries stop even if not
    /// expired yet. 1 disables retries. 0 means unlimited (up to the timeouts).</param>
    /// <param name="NonRetryableErrorTypes">Non-Retryable errors types. Will stop retrying if the error type matches
    /// this list. Note that this is not a substring match, the error *type* (not message) must match exactly.</param>
    public record RetryPolicy(TimeSpan InitialInterval,
                              double BackoffCoefficient,
                              TimeSpan MaximumInterval,
                              int MaximumAttempts,
                              ReadOnlyCollection<string> NonRetryableErrorTypes)
    {
    }

    public record RequestingWorkflowInfo(string Namespace,
                                         string WorkflowId,
                                         string RunId,
                                         string TypeName);

    public record ActivityTimestampInfo(DateTimeOffset Scheduled,
                                        DateTimeOffset CurrentAttemptScheduled,
                                        DateTimeOffset Started);

    public record ActivityTimeoutInfo(TimeSpan ScheduleToClose,
                                      TimeSpan StartToClose,
                                      TimeSpan Heartbeat);

    public interface IWorkflowActivityContext
    {
        ReadOnlyCollection<byte> ActivityTaskToken { get; }

#if NETCOREAPP3_1_OR_GREATER
        ReadOnlySpan<byte> ActivityTaskTokenBytes { get; }
#endif

        RequestingWorkflowInfo RequestingWorkflow { get; }

        string ActivityTypeName { get; }

        TDets GetLastAttemptHeartbeatDetails<TDets>();

        CancellationToken CancelToken { get; }

        ActivityTimestampInfo Times { get; }
        ActivityTimeoutInfo Timeouts { get; }

        int Attempt { get; }

        RetryPolicy RetryPolicy { get; }

        void RequestRecordHeartbeat();      // (?) SubmitHeartbeat() ? EnqueueHeartbeat() ?
        void RequestRecordHeartbeat<TArg>(TArg details);
    }

    internal interface IActivityExecutor
    {
        string ActivityTypeName { get; }
        Task<SerializedPayloads> CreateAndExecuteAsync(SerializedPayloads input,
                                                       IWorkflowActivityContext activityCtx,
                                                       TemporalActivityWorkerConfiguration workerConfig);
    }

    internal class ActivityExecutor<TArg, TResult> : IActivityExecutor
    {
        private readonly string _activityTypeName;
        private readonly IActivityImplementationFactory<TArg, TResult> _activityFactory;

        public ActivityExecutor(string activityTypeName,
                                IActivityImplementationFactory<TArg, TResult> activityFactory)
        {
            _activityTypeName = activityTypeName;
            _activityFactory = activityFactory;
        }

        public string ActivityTypeName { get { return _activityTypeName; } }

        public async Task<SerializedPayloads> CreateAndExecuteAsync(SerializedPayloads serializedInput,
                                                                    IWorkflowActivityContext activityCtx,
                                                                    TemporalActivityWorkerConfiguration workerConfig)
        {
            // POC. In reality here we will probably construct IWorkflowActivityContext from a worker context. 
            // Invocation paths will be different.

            IActivityImplementation<TArg, TResult> activity = _activityFactory.GetActivity();

            IPayloadConverter payloadConverter = CreatePayloadConverter(workerConfig);
            IPayloadCodec payloadCodec = CreatePayloadCodec(workerConfig);

            TArg input = await payloadConverter.DeserializeAsync<TArg>(payloadCodec, serializedInput, activityCtx.CancelToken);

            TResult output = await activity.ExecuteAsync(input, activityCtx);

            SerializedPayloads serializedOutput = await payloadConverter.SerializeAsync(payloadCodec, output, activityCtx.CancelToken);
            return serializedOutput;
        }

        private IPayloadConverter CreatePayloadConverter(TemporalActivityWorkerConfiguration workerConfig)
        {
            IPayloadConverter payloadConverter = null;
            Func<ActivityInvocationPipelineItemFactoryArguments, IPayloadConverter> customPayloadConverterFactory
                                                                                                = workerConfig.PayloadConverterFactory;
            if (customPayloadConverterFactory != null)
            {
                ActivityInvocationPipelineItemFactoryArguments converterFactoryArguments = new(_activityTypeName, _activityFactory);
                payloadConverter = customPayloadConverterFactory(converterFactoryArguments);
            }

            if (payloadConverter == null)
            {
                payloadConverter = new CompositePayloadConverter();
            }

            return payloadConverter;
        }

        private IPayloadCodec CreatePayloadCodec(TemporalActivityWorkerConfiguration workerConfig)
        {
            IPayloadCodec payloadCodec = null;
            Func<ActivityInvocationPipelineItemFactoryArguments, IPayloadCodec> customPayloadCodecFactory
                                                                                                = workerConfig.PayloadCodecFactory;
            if (customPayloadCodecFactory != null)
            {
                ActivityInvocationPipelineItemFactoryArguments codecFactoryArguments = new(_activityTypeName, _activityFactory);
                payloadCodec = customPayloadCodecFactory(codecFactoryArguments);
            }

            return payloadCodec;
        }
    }

    public interface IActivityImplementation<TArg, TResult>
    {
        Task<TResult> ExecuteAsync(TArg input, IWorkflowActivityContext activityCtx);
    }

    internal class ActivityAdapter<TArg, TResult> : IActivityImplementation<TArg, TResult>
    {
        private readonly Func<TArg, IWorkflowActivityContext, Task<TResult>> _activity;

        internal ActivityAdapter(Func<TArg, IWorkflowActivityContext, Task<TResult>> activity)
        {
            Validate.NotNull(activity);
            _activity = activity;
        }

        public Task<TResult> ExecuteAsync(TArg input, IWorkflowActivityContext activityCtx)
        {
            return _activity(input, activityCtx);
        }
    }

    public interface IActivityImplementationFactory<TArg, TResult>
    {
        string ActivityTypeName { get; }

        public IActivityImplementation<TArg, TResult> GetActivity();
    }

    public class BasicActivityImplementationFactory<TArg, TResult> : IActivityImplementationFactory<TArg, TResult>
    {
        #region Static APIs

        protected static string GetDefaultActivityTypeNameForImplementationType(Type activityImplementationType)
        {
            const string ActivityMonikerSuffix = "Activity";
            const char GenericParamsCountSeparator = '`';

            Validate.NotNull(activityImplementationType);

            if (!Types.ImplementsInterface(activityImplementationType, typeof(IActivityImplementation<,>)))
            {
                throw new ArgumentException($"The specified {nameof(activityImplementationType)} is exected"
                                          + $" to implement the iface `{typeof(IActivityImplementation<,>).Name}`,"
                                          + $" but that iface is not implemented by"
                                          + $" {nameof(activityImplementationType)}=`{activityImplementationType.ToString()}`.");
            }

            string activityTypeName = activityImplementationType.Name;

            // Remove the generic part from the type name (if any):
            // Example: "KeyValuePair`2". So threre must be a '`' and it must be followed by a valid number.

            int genMrkPos = activityTypeName.LastIndexOf(GenericParamsCountSeparator);
            if (genMrkPos > 0 && genMrkPos < activityTypeName.Length - 1)
            {
                string genNum = activityTypeName.Substring(genMrkPos + 1);
                if (Int32.TryParse(genNum, out _))
                {
                    activityTypeName = activityTypeName.Substring(0, genMrkPos);
                }
            }

            // Remove the trailing "Activity" suffix, if any:

            if (activityTypeName.EndsWith(ActivityMonikerSuffix, StringComparison.OrdinalIgnoreCase))
            {
                activityTypeName = activityTypeName.Substring(activityTypeName.Length - ActivityMonikerSuffix.Length);
            }

            return activityTypeName;
        }

        #endregion Static APIs

        private readonly string _activityTypeName;
        private readonly Func<IActivityImplementation<TArg, TResult>> _activityProvider;

        /// <summary>Creates a new <c>BasicActivityImplementationFactory</c>.</summary>
        /// <param name="activityTypeName">The Name of the Activity Type.</param>
        /// <param name="activityExec">A delegate that actually executes the activity.</param>
        public BasicActivityImplementationFactory(string activityTypeName,
                                                  Func<TArg, IWorkflowActivityContext, Task<TResult>> activityExec)
            : this(activityTypeName,
                   activityProvider: () => new ActivityAdapter<TArg, TResult>(activityExec))
        {
        }

        /// <summary>Creates a new <c>BasicActivityImplementationFactory</c>.</summary>
        /// <param name="activityTypeName">The Name of the Activity Type.</param>
        /// <param name="activityProvider">A delegate that, when invoked, returns an instnace of
        /// <c>IActivityImplementation</c> to be used for an activity invocation.</param>
        public BasicActivityImplementationFactory(string activityTypeName,
                                                  Func<IActivityImplementation<TArg, TResult>> activityProvider)
        {
            Validate.NotNull(activityTypeName);
            Validate.NotNull(activityProvider);

            _activityTypeName = activityTypeName;
            _activityProvider = activityProvider;
        }

        public virtual string ActivityTypeName
        {
            get { return _activityTypeName; }
        }

        public virtual IActivityImplementation<TArg, TResult> GetActivity()
        {
            return _activityProvider();
        }
    }

    public class AutoInstantiatingActivityImplementationFactory<TActImpl, TArg, TResult>
            : BasicActivityImplementationFactory<TArg, TResult>
            where TActImpl : IActivityImplementation<TArg, TResult>, new()
    {
        public AutoInstantiatingActivityImplementationFactory()
            : this(GetDefaultActivityTypeNameForImplementationType(typeof(TActImpl)))
        {
        }

        public AutoInstantiatingActivityImplementationFactory(string activityTypeName)
            : base(activityTypeName,
                   static () => new TActImpl())
        {
        }
    }

    public class InstanceSharingActivityImplementationFactory<TActImpl, TArg, TResult>
            : BasicActivityImplementationFactory<TArg, TResult>, IDisposable
            where TActImpl : IActivityImplementation<TArg, TResult>
    {
        private static IActivityImplementation<TArg, TResult> NullActivityInstanceProvider()
        {
            throw new InvalidOperationException("This method should mever be invoked in practice.");
        }

        private readonly TActImpl _sharedActivityImplementation;

        public InstanceSharingActivityImplementationFactory(TActImpl sharedActivityImplementation)
            : this(GetDefaultActivityTypeNameForImplementationType(typeof(TActImpl)),
                   sharedActivityImplementation)
        {
        }

        public InstanceSharingActivityImplementationFactory(string activityTypeName, TActImpl sharedActivityImplementation)
            : base(activityTypeName,
                   NullActivityInstanceProvider)
        {
            Validate.NotNull(sharedActivityImplementation);
            _sharedActivityImplementation = sharedActivityImplementation;
        }

        public override IActivityImplementation<TArg, TResult> GetActivity()
        {
            return _sharedActivityImplementation;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_sharedActivityImplementation is IDisposable disposableSharedActivityImplementation)
            {
                if (disposing)
                {
                    disposableSharedActivityImplementation.Dispose();
                }
                else
                {
                    try // Do not throw on Finalizer thread.
                    {
                        disposableSharedActivityImplementation.Dispose();
                    }
                    catch { }
                }
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    internal static class Types
    {
        public static bool HasInterface<T>(T instance, Type ifaceType)
        {
            return (instance == null)
                    ? ImplementsInterface(typeof(T), ifaceType)
                    : ImplementsInterface(instance.GetType(), ifaceType);
        }

        public static bool ImplementsInterface(Type someType, Type ifaceType)
        {
            if (false == ifaceType.IsInterface)
            {
                return false;
            }

            if (ifaceType.IsAssignableFrom(someType))
            {
                return true;
            }

            Type[] ifaces = someType.GetInterfaces();
            for (int i = 0; i < ifaces.Length; i++)
            {
                if (ifaces[i].IsGenericType && ifaces[i].GetGenericTypeDefinition() == ifaceType)
                {
                    return true;
                }
            }

            return false;
        }
    }
}  // Temporal.Worker.Hosting


namespace Temporal.Common.Payloads2
{
    public static partial class Payload
    {
#if NETCOREAPP3_1_OR_GREATER
        public static PayloadContainers.INamed Named<T>(params (string name, T value)[] namedValues)
        {
            throw new NotImplementedException("@ToDo");
        }
#endif

        public static PayloadContainers.INamed Named(params object[] namedValues)
        {
            // Remember: Deal with the absense of tuples on Net4.x by failing fast based on name of element.
            // Advise user to remove braces at callsite to get a normal array or to use key-value-pairs
            throw new NotImplementedException("@ToDo");
        }

        public static PayloadContainers.INamed Named<T>(IEnumerable<KeyValuePair<string, T>> namedValues)
        {
            throw new NotImplementedException("@ToDo");
        }
    }

    public static partial class PayloadContainers
    {
        public interface INamed : IPayload
        {
            int Count { get; }
            TVal GetValue<TVal>(string name);
            bool TryGetValue<TVal>(int name, out TVal value);
        }
    }
}  // namespace Temporal.Common.Payloads
