using System.Collections.Generic;

namespace Temporalio.Extensions.ToolRegistry.Testing
{
    /// <summary>
    /// Records a single tool invocation on <see cref="FakeToolRegistry"/>.
    /// </summary>
    /// <param name="Name">Tool name that was dispatched.</param>
    /// <param name="Input">Input that was passed to the tool.</param>
    /// <param name="Result">String value returned by the tool handler.</param>
    public sealed record DispatchCall(
        string Name,
        IReadOnlyDictionary<string, object?> Input,
        string Result);
}
