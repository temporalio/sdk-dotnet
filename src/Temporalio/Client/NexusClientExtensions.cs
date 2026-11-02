using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using NexusRpc.Handlers;
using Temporalio.Common;

namespace Temporalio.Client
{
    /// <summary>
    /// Client methods for standalone Nexus operations with typed completion callbacks.
    /// </summary>
    public static class NexusClientExtensions
    {
        /// <summary>
        /// Starts a void-returning Nexus operation with typed completion callbacks.
        /// </summary>
        /// <typeparam name="TService">The source service interface.</typeparam>
        /// <param name="client">The Nexus client.</param>
        /// <param name="operationStartCall">An expression that calls a no-value source operation.</param>
        /// <param name="options">The operation options and completion callbacks.</param>
        /// <returns>A handle to the started operation.</returns>
        /// <exception cref="ArgumentException">The expression does not select a registered no-value operation.</exception>
        /// <remarks>WARNING: This API is experimental and may change.</remarks>
        public static Task<NexusOperationHandle> StartNexusOperationAsync<TService>(
            this NexusClient<TService> client,
            Expression<Action<TService>> operationStartCall,
            NexusOperationOptions<NoValue> options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (operationStartCall == null)
            {
                throw new ArgumentNullException(nameof(operationStartCall));
            }
            var (method, args) = ExpressionUtil.ExtractCall(operationStartCall);
            var operation = client.ServiceDefinition.Operations.Values.FirstOrDefault(value => value.MethodInfo == method);
            if (operation == null)
            {
                throw new ArgumentException(
                    $"Method {method} not marked as a Nexus service operation", nameof(operationStartCall));
            }
            if (!NoValue.IsVoidType(operation.OutputType))
            {
                throw new ArgumentException(
                    $"Operation {operation.Name} returns {operation.OutputType}, but the options require a no-value operation",
                    nameof(operationStartCall));
            }
            if (args.Count > 1)
            {
                throw new ArgumentException("Can only have 0 or 1 Nexus argument", nameof(operationStartCall));
            }
            return client.StartNexusOperationAsync(operation.Name, args.SingleOrDefault(), options.ToUntyped());
        }

        /// <summary>
        /// Runs a void-returning Nexus operation with typed completion callbacks and waits for completion.
        /// </summary>
        /// <typeparam name="TService">The source service interface.</typeparam>
        /// <param name="client">The Nexus client.</param>
        /// <param name="operationStartCall">An expression that calls a no-value source operation.</param>
        /// <param name="options">The operation options and completion callbacks.</param>
        /// <returns>A task representing completion of the operation.</returns>
        /// <exception cref="ArgumentException">The expression does not select a registered no-value operation.</exception>
        /// <remarks>WARNING: This API is experimental and may change.</remarks>
        public static async Task ExecuteNexusOperationAsync<TService>(
            this NexusClient<TService> client,
            Expression<Action<TService>> operationStartCall,
            NexusOperationOptions<NoValue> options)
        {
            var handle = await client.StartNexusOperationAsync(operationStartCall, options).ConfigureAwait(false);
            await handle.GetResultAsync(options.Rpc).ConfigureAwait(false);
        }

        /// <summary>
        /// Starts a named Nexus operation with typed completion callbacks.
        /// </summary>
        /// <typeparam name="TOutput">The type of the operation's result.</typeparam>
        /// <param name="client">The Nexus client.</param>
        /// <param name="operationName">The name of the source operation.</param>
        /// <param name="arg">The argument for the source operation.</param>
        /// <param name="options">The operation options and completion callbacks.</param>
        /// <returns>A handle to the started operation.</returns>
        /// <remarks>WARNING: This API is experimental and may change.</remarks>
        public static Task<NexusOperationHandle<TOutput>> StartNexusOperationAsync<TOutput>(
            this NexusClient client,
            string operationName,
            object? arg,
            NexusOperationOptions<TOutput> options) =>
            client.StartNexusOperationAsync<TOutput>(
                operationName,
                arg,
                (options ?? throw new ArgumentNullException(nameof(options))).ToUntyped());

        /// <summary>
        /// Runs a named Nexus operation with typed completion callbacks and waits for its result.
        /// </summary>
        /// <typeparam name="TOutput">The type of the operation's result.</typeparam>
        /// <param name="client">The Nexus client.</param>
        /// <param name="operationName">The name of the source operation.</param>
        /// <param name="arg">The argument for the source operation.</param>
        /// <param name="options">The operation options and completion callbacks.</param>
        /// <returns>The result of the operation.</returns>
        /// <remarks>WARNING: This API is experimental and may change.</remarks>
        public static Task<TOutput> ExecuteNexusOperationAsync<TOutput>(
            this NexusClient client,
            string operationName,
            object? arg,
            NexusOperationOptions<TOutput> options) =>
            client.ExecuteNexusOperationAsync<TOutput>(
                operationName,
                arg,
                (options ?? throw new ArgumentNullException(nameof(options))).ToUntyped());

        /// <summary>
        /// Starts a Nexus operation with completion callbacks for its result type.
        /// </summary>
        /// <typeparam name="TService">The source service interface.</typeparam>
        /// <typeparam name="TOutput">The type of the operation's result.</typeparam>
        /// <param name="client">The Nexus client.</param>
        /// <param name="operationStartCall">An expression that calls the source operation.</param>
        /// <param name="options">The operation options and completion callbacks.</param>
        /// <returns>A handle to the started operation.</returns>
        /// <remarks>WARNING: This API is experimental and may change.</remarks>
        public static Task<NexusOperationHandle<TOutput>> StartNexusOperationAsync<TService, TOutput>(
            this NexusClient<TService> client,
            Expression<Func<TService, TOutput>> operationStartCall,
            NexusOperationOptions<TOutput> options) =>
            client.StartNexusOperationAsync(
                operationStartCall,
                (options ?? throw new ArgumentNullException(nameof(options))).ToUntyped());

        /// <summary>
        /// Runs a Nexus operation with typed completion callbacks and waits for its result.
        /// </summary>
        /// <typeparam name="TService">The source service interface.</typeparam>
        /// <typeparam name="TOutput">The type of the operation's result.</typeparam>
        /// <param name="client">The Nexus client.</param>
        /// <param name="operationStartCall">An expression that calls the source operation.</param>
        /// <param name="options">The operation options and completion callbacks.</param>
        /// <returns>The result of the operation.</returns>
        /// <remarks>WARNING: This API is experimental and may change.</remarks>
        public static Task<TOutput> ExecuteNexusOperationAsync<TService, TOutput>(
            this NexusClient<TService> client,
            Expression<Func<TService, TOutput>> operationStartCall,
            NexusOperationOptions<TOutput> options) =>
            client.ExecuteNexusOperationAsync(
                operationStartCall,
                (options ?? throw new ArgumentNullException(nameof(options))).ToUntyped());
    }
}
