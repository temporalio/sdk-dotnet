namespace Temporalio.Tests.Worker;

using Xunit;
using Xunit.Abstractions;

public class ActivityWorkerTests : WorkflowEnvironmentTestBase
{
    public ActivityWorkerTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_SimpleStaticMethod_Succeeds()
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_SimpleInstanceMethod_Succeeds()
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_SimpleLambda_Succeeds()
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_CheckInfo_IsAccurate()
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_Throw_ReportsFailure()
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_BadParamConversion_ReportsFailure()
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_CalledWithTooFewParams_Throws()
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_CalledWithoutDefaultParams_UsesDefaults()
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_CalledWithTooManyParams_IgnoresExtra()
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_ServerCancel_ReportsCancel()
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_CaughtServerCancel_Succeeds()
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_WorkerShutdown_ReportsFailure()
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_ThrowsOperationCancelled_ReportsFailure()
    {
        // Just to confirm there's not an issue with this exception
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_UnknownActivity_ReportsFailure()
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_MaxConcurrent_TimesOutIfMore()
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_HeartbeatDetails_ProperlyRecorded()
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_HeartbeatDetailsConversionFailure_ReportsFailure()
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_Logging_IncludesScope()
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_AsyncCompletion_Succeeds()
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_AsyncCompletionHeartbeat_ProperlyRecorded()
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteActivityAsync_AsyncCompletionCancel_ReportsCancel()
    {
    }

    [Fact(Skip = "TODO")]
    public void ExecuteAsync_PollFailure_ShutsDownWorker()
    {
    }

    [Fact(Skip = "TODO")]
    public void New_DuplicateActivityNames_Throws()
    {
    }
}