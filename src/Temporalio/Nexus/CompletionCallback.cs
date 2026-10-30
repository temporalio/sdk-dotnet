#pragma warning disable SA1402

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using NexusRpc;
using Temporalio.Converters;
using Temporalio.Nexus.Notifications;
using CallbackProto = Temporalio.Api.Common.V1.Callback;

namespace Temporalio.Nexus
{
    /// <summary>
    /// A callback that reports the result or failure of a Nexus operation.
    /// </summary>
    /// <remarks>WARNING: This API is experimental and may change.</remarks>
    public abstract class CompletionCallback
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CompletionCallback"/> class.
        /// </summary>
        /// <param name="service">The name of the callback service.</param>
        /// <param name="operation">The name of the callback operation.</param>
        /// <param name="taskQueue">The task queue that runs the callback operation.</param>
        /// <param name="sourceContext">The source context that the callback operation receives.</param>
        private protected CompletionCallback(
            string service, string operation, string taskQueue, object? sourceContext)
        {
            Service = !string.IsNullOrWhiteSpace(service)
                ? service : throw new ArgumentException("Service must not be empty", nameof(service));
            Operation = !string.IsNullOrWhiteSpace(operation)
                ? operation : throw new ArgumentException("Operation must not be empty", nameof(operation));
            TaskQueue = !string.IsNullOrWhiteSpace(taskQueue)
                ? taskQueue : throw new ArgumentException("Task queue must not be empty", nameof(taskQueue));
            SourceContext = sourceContext;
        }

        /// <summary>
        /// Gets the name of the callback service.
        /// </summary>
        public string Service { get; }

        /// <summary>
        /// Gets the name of the callback operation.
        /// </summary>
        public string Operation { get; }

        /// <summary>
        /// Gets the callback task queue in the namespace of the client that starts the Nexus operation.
        /// </summary>
        public string TaskQueue { get; }

        /// <summary>
        /// Gets the source context that the callback operation receives.
        /// </summary>
        public object? SourceContext { get; }

        /// <summary>
        /// Gets the type of the source operation's result accepted by this callback.
        /// </summary>
        internal abstract Type OutputType { get; }

        /// <summary>
        /// Creates a callback from service and operation names.
        /// </summary>
        /// <param name="service">The name of the callback service.</param>
        /// <param name="operation">The name of the callback operation.</param>
        /// <param name="taskQueue">The task queue that runs the callback operation.</param>
        /// <param name="sourceContext">The source context that the callback operation receives.</param>
        /// <returns>A callback that can be added to any output collection.</returns>
        /// <remarks>WARNING: This API is experimental and may change.</remarks>
        public static CompletionCallback<RawValue> Create(
            string service, string operation, string taskQueue, object? sourceContext) =>
            new(service, operation, taskQueue, sourceContext);

        /// <summary>
        /// Creates a callback from an operation expression without calling the operation.
        /// </summary>
        /// <typeparam name="TService">The callback service interface.</typeparam>
        /// <typeparam name="TOutput">The type of the source operation's result.</typeparam>
        /// <typeparam name="TSourceContext">The type of the source context.</typeparam>
        /// <param name="operationCall">
        /// An expression that selects the callback operation.
        /// The request parameter must be the operation's only argument.
        /// </param>
        /// <param name="taskQueue">The task queue that runs the callback operation.</param>
        /// <param name="sourceContext">The source context that the callback operation receives.</param>
        /// <returns>The callback for the selected operation.</returns>
        /// <remarks>WARNING: This API is experimental and may change.</remarks>
        public static CompletionCallback<TOutput> Create<TService, TOutput, TSourceContext>(
            Expression<Func<TService, OnCompleteRequest<TOutput, TSourceContext>, OnCompleteResponse>>
                operationCall,
            string taskQueue,
            TSourceContext sourceContext)
        {
            if (operationCall == null)
            {
                throw new ArgumentNullException(nameof(operationCall));
            }
            if (operationCall.Body is not MethodCallExpression call ||
                call.Object != operationCall.Parameters[0] ||
                call.Arguments.Count != 1 ||
                call.Arguments[0] != operationCall.Parameters[1])
            {
                throw new ArgumentException(
                    "Expression must call a service operation with the request parameter directly",
                    nameof(operationCall));
            }

            var service = ServiceDefinition.FromType<TService>();
            var operation = service.Operations.Values.FirstOrDefault(
                value => value.MethodInfo == call.Method);
            if (operation == null ||
                operation.InputType != typeof(OnCompleteRequest<TOutput, TSourceContext>) ||
                operation.OutputType != typeof(OnCompleteResponse))
            {
                throw new ArgumentException(
                    "Expression must reference a registered Nexus completion operation with exact request and response types",
                    nameof(operationCall));
            }
            return new CompletionCallback<TOutput>(
                service.Name, operation.Name, taskQueue, sourceContext);
        }

        /// <summary>
        /// Converts the callback to a protobuf message with the supplied data converter.
        /// </summary>
        /// <param name="converter">The data converter that encodes the source context.</param>
        /// <returns>The protobuf callback message.</returns>
        internal async Task<CallbackProto> ToProtoAsync(DataConverter converter) =>
            new()
            {
                NexusHandler = new()
                {
                    Service = Service,
                    Operation = Operation,
                    TaskQueueName = TaskQueue,
                    SourceContext = await converter.ToPayloadAsync(SourceContext).ConfigureAwait(false),
                },
            };
    }

    /// <summary>
    /// A completion callback that accepts a specific operation output type.
    /// </summary>
    /// <typeparam name="TOutput">The type of the source operation's result.</typeparam>
    /// <remarks>
    /// Create callbacks with <see cref="CompletionCallback.Create{TService, TOutput, TSourceContext}"/>.
    /// Callbacks that accept <see cref="Converters.RawValue"/>
    /// can be added to a callback collection for any output type.
    /// WARNING: This API is experimental and may change.
    /// </remarks>
    public sealed class CompletionCallback<TOutput> : CompletionCallback
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CompletionCallback{TOutput}"/> class.
        /// </summary>
        /// <param name="service">The name of the callback service.</param>
        /// <param name="operation">The name of the callback operation.</param>
        /// <param name="taskQueue">The task queue that runs the callback operation.</param>
        /// <param name="sourceContext">The source context that the callback operation receives.</param>
        internal CompletionCallback(
            string service, string operation, string taskQueue, object? sourceContext)
            : base(service, operation, taskQueue, sourceContext)
        {
        }

        /// <inheritdoc />
        internal override Type OutputType => typeof(TOutput);
    }
}
