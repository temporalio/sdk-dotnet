using System;
using System.Collections.Generic;

namespace Temporalio.Extensions.ToolRegistry.Testing
{
    /// <summary>
    /// Wraps <see cref="ToolRegistry"/> and records every <see cref="Dispatch"/> call.
    /// Implements <see cref="IDispatcher"/> for use with <see cref="MockProvider.WithRegistry(IDispatcher)"/>.
    /// </summary>
    /// <remarks>
    /// Example:
    /// <code>
    /// var fake = new FakeToolRegistry();
    /// fake.Register(def, input => "result");
    /// // Use fake.WithRegistry(fake) on MockProvider to record calls.
    /// Assert.Single(fake.Calls);
    /// Assert.Equal("tool-name", fake.Calls[0].Name);
    /// </code>
    /// </remarks>
    public sealed class FakeToolRegistry : IDispatcher
    {
        private readonly ToolRegistry inner = new();
        private readonly List<DispatchCall> calls = new();

        /// <summary>
        /// Gets all tool dispatch invocations in order.
        /// </summary>
        public IList<DispatchCall> Calls => calls;

        /// <summary>
        /// Registers a tool definition and its handler.
        /// </summary>
        /// <param name="def">Tool definition.</param>
        /// <param name="handler">Function called when the tool is dispatched.</param>
        public void Register(ToolDef def, Func<IReadOnlyDictionary<string, object?>, string> handler) =>
            inner.Register(def, handler);

        /// <summary>
        /// Records the call and delegates dispatch to the underlying registry.
        /// </summary>
        /// <param name="name">Tool name.</param>
        /// <param name="input">Tool input.</param>
        /// <returns>String result from the handler.</returns>
        public string Dispatch(string name, IReadOnlyDictionary<string, object?> input)
        {
            var result = inner.Dispatch(name, input);
            calls.Add(new(name, input, result));
            return result;
        }

        /// <summary>
        /// Returns the underlying registry's definitions.
        /// </summary>
        /// <returns>Read-only list of registered tool definitions.</returns>
        public IReadOnlyList<ToolDef> Definitions() => inner.Definitions();
    }
}
