namespace Temporalio.Tests;

using System.Text.Json.Serialization;
using Temporalio.Workflows;

[Workflow("kitchen_sink")]
public interface IKitchenSinkWorkflow
{
    [WorkflowRun]
    Task<string> RunAsync(KSWorkflowParams args);

    [WorkflowSignal]
    Task SomeActionSignalAsync(KSAction action);

    [WorkflowSignal]
    Task SomeSignalAsync(string arg);

    [WorkflowQuery]
    string SomeQuery(string arg);
}

[Workflow("kitchen_sink")]
public interface IKitchenSinkWorkflowWithReturnObject
{
    [WorkflowRun]
    Task<KSWorkflowResult> RunAsync(KSWorkflowParams args);
}

[Workflow("kitchen_sink")]
public interface IKitchenSinkWorkflowWithUnknownReturn
{
    [WorkflowRun]
    Task<string> RunAsync(KSWorkflowParams args);
}

public record KSWorkflowResult(string SomeString);

public record KSWorkflowParams(
    [property: JsonPropertyName("actions")] IReadOnlyCollection<KSAction>? Actions = null,
    [property: JsonPropertyName("action_signal")] string? ActionSignal = null)
{
    public KSWorkflowParams(params KSAction[] actions)
        : this(Actions: actions)
    {
    }

    public KSWorkflowParams()
        : this(Actions: null)
    {
    }
}

public record KSAction(
    [property: JsonPropertyName("result")] KSResultAction? Result = null,
    [property: JsonPropertyName("error")] KSErrorAction? Error = null,
    [property: JsonPropertyName("continue_as_new")] KSContinueAsNewAction? ContinueAsNew = null,
    [property: JsonPropertyName("sleep")] KSSleepAction? Sleep = null,
    [property: JsonPropertyName("query_handler")] KSQueryHandlerAction? QueryHandler = null,
    [property: JsonPropertyName("signal")] KSSignalAction? Signal = null,
    [property: JsonPropertyName("execute_activity")] KSExecuteActivityAction? ExecuteActivity = null);

public record KSResultAction(
    [property: JsonPropertyName("value")] object? Value = null,
    [property: JsonPropertyName("run_id")] bool RunId = false);

public record KSErrorAction(
    [property: JsonPropertyName("message")] string? Message = null,
    [property: JsonPropertyName("details")] object? Details = null,
    [property: JsonPropertyName("attempt")] bool Attempt = false);

public record KSContinueAsNewAction(
    [property: JsonPropertyName("while_above_zero")] int? WhileAboveZero = null,
    [property: JsonPropertyName("result")] object? Result = null);

public record KSSleepAction(
    [property: JsonPropertyName("millis")] long Millis);

public record KSQueryHandlerAction(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("error")] string? Error = null);

public record KSSignalAction(
    [property: JsonPropertyName("name")] string Name);

public record KSExecuteActivityAction(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("task_queue")] string? TaskQueue = null,
    [property: JsonPropertyName("args")] IReadOnlyCollection<object?>? Args = null,
    [property: JsonPropertyName("count")] int? Count = null,
    [property: JsonPropertyName("index_as_arg")] bool IndexAsArg = false,
    [property: JsonPropertyName("schedule_to_close_timeout_ms")] long? ScheduleToCloseTimeoutMS = null,
    [property: JsonPropertyName("start_to_close_timeout_ms")] long? StartToCloseTimeoutMS = null,
    [property: JsonPropertyName("schedule_to_start_timeout_ms")] long? ScheduleToStartTimeoutMS = null,
    [property: JsonPropertyName("cancel_after_ms")] long? CancelAfterMS = null,
    [property: JsonPropertyName("wait_for_cancellation")] bool WaitForCancellation = false,
    [property: JsonPropertyName("heartbeat_timeout_ms")] long? HeartbeatTimeoutMS = null,
    [property: JsonPropertyName("retry_max_attempts")] int? RetryMaxAttempts = null,
    [property: JsonPropertyName("non_retryable_error_types")] IReadOnlyCollection<string>? NonRetryableErrorTypes = null);
