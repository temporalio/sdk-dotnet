using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using Temporalio.Converters;
using Temporalio.Nexus.Notifications;

namespace Temporalio.Nexus
{
    /// <summary>
    /// An immutable collection of callbacks that accept the source result type or raw payloads.
    /// Use a <see cref="Builder"/> to create a nonempty collection.
    /// </summary>
    /// <typeparam name="TOutput">The type of the source operation's result.</typeparam>
    /// <remarks>WARNING: This API is experimental and may change.</remarks>
    public sealed class CompletionCallbackCollection<TOutput> : IReadOnlyCollection<CompletionCallback>
    {
        private readonly List<CompletionCallback> callbacks;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompletionCallbackCollection{TOutput}"/> class.
        /// </summary>
        public CompletionCallbackCollection()
            : this(new())
        {
        }

        private CompletionCallbackCollection(List<CompletionCallback> callbacks) =>
            this.callbacks = callbacks;

        /// <inheritdoc/>
        public int Count => callbacks.Count;

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

            var result = new List<CompletionCallback>(this.callbacks);
            result.AddRange(callbacks);
            return new(result);
        }

        /// <summary>
        /// Builder for an immutable callback collection. This builder is not thread safe.
        /// </summary>
        public sealed class Builder : IEnumerable<CompletionCallback>
        {
            private readonly List<CompletionCallback> callbacks = new();

            /// <summary>
            /// Initializes a new instance of the <see cref="Builder"/> class.
            /// </summary>
            public Builder()
            {
            }

            /// <summary>
            /// Adds a callback that accepts the source operation's result type.
            /// </summary>
            /// <param name="callback">The callback to add.</param>
            /// <returns>This builder.</returns>
            public Builder Add(CompletionCallback<TOutput> callback)
            {
                callbacks.Add(callback ?? throw new ArgumentNullException(nameof(callback)));
                return this;
            }

            /// <summary>
            /// Adds a callback that accepts raw payloads.
            /// </summary>
            /// <param name="callback">The callback to add.</param>
            /// <returns>This builder.</returns>
            public Builder Add(CompletionCallback<RawValue> callback)
            {
                callbacks.Add(callback ?? throw new ArgumentNullException(nameof(callback)));
                return this;
            }

            /// <summary>
            /// Creates and adds a callback that accepts the source operation's result type.
            /// </summary>
            /// <typeparam name="TService">The callback service interface.</typeparam>
            /// <typeparam name="TSourceContext">The type of the source context.</typeparam>
            /// <param name="operationCall">An expression that selects the callback operation without calling it.</param>
            /// <param name="taskQueue">The task queue that runs the callback operation.</param>
            /// <param name="sourceContext">The source context that the callback operation receives.</param>
            /// <returns>This builder.</returns>
            public Builder Add<TService, TSourceContext>(
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
            /// <returns>This builder.</returns>
            public Builder Add<TService, TSourceContext>(
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
            /// <returns>This builder.</returns>
            public Builder Add(string service, string operation, string taskQueue, object? sourceContext) =>
                Add(CompletionCallback.Create(service, operation, taskQueue, sourceContext));

            /// <summary>
            /// Creates an immutable snapshot of this builder's callbacks.
            /// </summary>
            /// <returns>A collection that is not affected by subsequent changes to this builder.</returns>
            public CompletionCallbackCollection<TOutput> Build() => new(new(callbacks));

            /// <inheritdoc/>
            public IEnumerator<CompletionCallback> GetEnumerator() => callbacks.GetEnumerator();

            /// <inheritdoc/>
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
