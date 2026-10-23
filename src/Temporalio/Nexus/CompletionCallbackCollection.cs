using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using Temporalio.Converters;
using Temporalio.Nexus.Notifications;

namespace Temporalio.Nexus
{
    /// <summary>
    /// A collection of callbacks that accept the source result type or raw payloads.
    /// </summary>
    /// <typeparam name="TOutput">The type of the source operation's result.</typeparam>
    /// <remarks>WARNING: This API is experimental and may change.</remarks>
    public sealed class CompletionCallbackCollection<TOutput> : IReadOnlyCollection<CompletionCallback>
    {
        private readonly List<CompletionCallback> callbacks = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="CompletionCallbackCollection{TOutput}"/> class.
        /// </summary>
        public CompletionCallbackCollection()
        {
        }

        /// <inheritdoc/>
        public int Count => callbacks.Count;

        /// <summary>
        /// Adds a callback that accepts the source operation's result type.
        /// </summary>
        /// <param name="callback">The callback to add.</param>
        public void Add(CompletionCallback<TOutput> callback) =>
            callbacks.Add(callback ?? throw new ArgumentNullException(nameof(callback)));

        /// <summary>
        /// Adds a callback that accepts raw payloads.
        /// </summary>
        /// <param name="callback">The callback to add.</param>
        public void Add(CompletionCallback<RawValue> callback) =>
            callbacks.Add(callback ?? throw new ArgumentNullException(nameof(callback)));

        /// <summary>
        /// Creates and adds a callback that accepts the source operation's result type.
        /// </summary>
        /// <typeparam name="TService">The callback service interface.</typeparam>
        /// <typeparam name="TSourceContext">The type of the source context.</typeparam>
        /// <param name="operationCall">An expression that selects the callback operation without calling it.</param>
        /// <param name="taskQueue">The task queue that runs the callback operation.</param>
        /// <param name="sourceContext">The source context that the callback operation receives.</param>
        public void Add<TService, TSourceContext>(
            Expression<Func<TService, OnCompleteRequest<TOutput, TSourceContext>, OnCompleteResponse>> operationCall,
            string taskQueue,
            TSourceContext sourceContext) =>
            Add(CompletionCallback.Create(operationCall, taskQueue, sourceContext));

        /// <summary>
        /// Creates and adds a callback that accepts raw payloads.
        /// </summary>
        /// <typeparam name="TService">The callback service interface.</typeparam>
        /// <typeparam name="TSourceContext">The type of the source context.</typeparam>
        /// <param name="operationCall">An expression that selects the callback operation without calling it.</param>
        /// <param name="taskQueue">The task queue that runs the callback operation.</param>
        /// <param name="sourceContext">The source context that the callback operation receives.</param>
        public void Add<TService, TSourceContext>(
            Expression<Func<TService, OnCompleteRequest<RawValue, TSourceContext>, OnCompleteResponse>> operationCall,
            string taskQueue,
            TSourceContext sourceContext) =>
            Add(CompletionCallback.Create(operationCall, taskQueue, sourceContext));

        /// <summary>
        /// Creates and adds a callback from service and operation names.
        /// </summary>
        /// <param name="service">The name of the callback service.</param>
        /// <param name="operation">The name of the callback operation.</param>
        /// <param name="taskQueue">The task queue that runs the callback operation.</param>
        /// <param name="sourceContext">The source context that the callback operation receives.</param>
        public void Add(string service, string operation, string taskQueue, object? sourceContext) =>
            Add(CompletionCallback.Create(service, operation, taskQueue, sourceContext));

        /// <summary>
        /// Combines these callbacks with another collection without changing either source.
        /// </summary>
        /// <param name="callbacks">The callbacks to append.</param>
        /// <returns>A new collection with these callbacks followed by the supplied callbacks.</returns>
        public CompletionCallbackCollection<TOutput> Concat(CompletionCallbackCollection<TOutput> callbacks) =>
            ConcatCore(callbacks);

        /// <summary>
        /// Combines these callbacks with raw callbacks without changing either source.
        /// </summary>
        /// <param name="callbacks">The callbacks to append.</param>
        /// <returns>A new collection with these callbacks followed by the supplied callbacks.</returns>
        public CompletionCallbackCollection<TOutput> Concat(CompletionCallbackCollection<RawValue> callbacks) =>
            ConcatCore(callbacks);

        /// <inheritdoc/>
        public IEnumerator<CompletionCallback> GetEnumerator() => callbacks.GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private CompletionCallbackCollection<TOutput> ConcatCore(IReadOnlyCollection<CompletionCallback> callbacks)
        {
            if (callbacks == null)
            {
                throw new ArgumentNullException(nameof(callbacks));
            }

            var result = new CompletionCallbackCollection<TOutput>();
            result.callbacks.AddRange(this.callbacks);
            result.callbacks.AddRange(callbacks);
            return result;
        }
    }
}
