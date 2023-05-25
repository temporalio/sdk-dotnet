using System;
using Temporalio.Common;

namespace Temporalio.Worker
{
    /// <summary>
    /// Result of a workflow replay from <see cref="WorkflowReplayer" />.
    /// </summary>
    /// <param name="History">History that was run.</param>
    /// <param name="ReplayFailure">Workflow task failure during replay (e.g. nondeterminism). Note,
    /// this is not a normal workflow failure which is not currently captured by replayer.</param>
    public record WorkflowReplayResult(
        WorkflowHistory History,
        Exception? ReplayFailure);
}