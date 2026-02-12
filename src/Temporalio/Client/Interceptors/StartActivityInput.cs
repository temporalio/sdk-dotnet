using System.Collections.Generic;
using Temporalio.Api.Common.V1;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.StartActivityAsync" />.
    /// </summary>
    /// <param name="Activity">Activity type name to start.</param>
    /// <param name="Args">Arguments for the activity.</param>
    /// <param name="Options">Options passed in to start.</param>
    /// <param name="Headers">Headers to include for activity start. These will be encoded using the
    /// codec before sent to the server.</param>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record StartActivityInput(
        string Activity,
        IReadOnlyCollection<object?> Args,
        StartActivityOptions Options,
        IDictionary<string, Payload>? Headers);
}
