using System.Collections.Generic;

namespace Temporalio.Extensions.ToolRegistry.Testing
{
    /// <summary>
    /// Implemented by <see cref="ToolRegistry"/> and <see cref="FakeToolRegistry"/>.
    /// Pass a <see cref="FakeToolRegistry"/> to <see cref="MockProvider.WithRegistry(IDispatcher)"/> to record
    /// which tool calls the scripted responses trigger.
    /// </summary>
    public interface IDispatcher
    {
        /// <summary>
        /// Dispatches a tool call by name and returns the string result.
        /// </summary>
        /// <param name="name">Tool name.</param>
        /// <param name="input">Tool input.</param>
        /// <returns>String result.</returns>
        string Dispatch(string name, IReadOnlyDictionary<string, object?> input);
    }
}
