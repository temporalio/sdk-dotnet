using System;
using System.Threading;
using System.Threading.Tasks;
using Temporal.Api.Enums.V1;
using Temporal.Common;
using Temporal.TestUtil;
using Temporal.Util;
using Temporal.WorkflowClient;
using Temporal.WorkflowClient.Errors;
using Xunit;
using Xunit.Abstractions;

namespace Temporal.Sdk.WorkflowClient.Test.E2EInt
{
    [Collection("SequentialTestExecution")]
    public abstract class SimpleClientInvocationsE2ETestBase : IntegrationTestBase
    {
        protected SimpleClientInvocationsE2ETestBase(ITestOutputHelper cout, TestTlsOptions options, int port)
            : base(cout, port, options)
        {
        }

        [Fact]
        public async Task E2EScenarioAsync()
        {
            string channelType = TlsOptions == TestTlsOptions.None ? "plain unsecured" : "tls secured";
            CoutWriteLine($"Creating a client over a ({channelType}) channel...");

            ITemporalClient client = await InitTemporalClient();

            string demoWfId = TestCaseContextMonikers.ForWorkflowId(this);
            string demoTaskQueue = TestCaseContextMonikers.ForTaskQueue(this);

            CoutWriteLine("Starting a workflow...");
            IWorkflowHandle workflow = await client.StartWorkflowAsync(demoWfId,
                                                       "DemoWorkflowTypeName",
                                                                       demoTaskQueue);
            CoutWriteLine("Started. Info:");
            CoutWriteLine($"    Namespace:       {workflow.Namespace}");
            CoutWriteLine($"    WorkflowId:      {workflow.WorkflowId}");
            CoutWriteLine($"    IsBound:         {workflow.IsBound}");
            CoutWriteLine($"    WorkflowChainId: {workflow.WorkflowChainId}");

            CoutWriteLine();
            CoutWriteLine("Attempting to start a workflow with the same WorkflowId...");
            CoutWriteLine();

            try
            {
                await client.StartWorkflowAsync(demoWfId, "DemoWorkflowTypeName", demoTaskQueue);

                throw new Exception("ERROR. We should never get here, because the above code is expected to throw.");
            }
            catch (WorkflowAlreadyExistsException waeEx)
            {
                CoutWriteLine("Received expected exception.");
                CoutWriteLine(waeEx.TypeAndMessage());

                Exception innerEx = waeEx.InnerException;
                while (innerEx != null)
                {
                    CoutWriteLine("\n  Inner --> " + innerEx.TypeAndMessage());
                    innerEx = innerEx.InnerException;
                }
            }

            CoutWriteLine();
            CoutWriteLine("Creating a handle to the existing workflow...");
            CoutWriteLine();

            IWorkflowHandle workflow2 = client.CreateWorkflowHandle(demoWfId);

            CoutWriteLine("Created. Info:");
            CoutWriteLine($"    Namespace:       {workflow2.Namespace}");
            CoutWriteLine($"    WorkflowId:      {workflow2.WorkflowId}");
            CoutWriteLine($"    IsBound:         {workflow2.IsBound}");

            try
            {
                CoutWriteLine($"    WorkflowChainId: {workflow2.WorkflowChainId}");

                throw new Exception("ERROR. We should never get here, because the above code is expected to throw.");
            }
            catch (InvalidOperationException invOpEx)
            {
                CoutWriteLine($"    Expected exception while getting {nameof(workflow2.WorkflowChainId)}:");
                CoutWriteLine($"    --> {invOpEx.TypeAndMessage()}");

                Exception innerEx = invOpEx.InnerException;
                while (innerEx != null)
                {
                    CoutWriteLine("\n      Inner --> " + invOpEx.TypeAndMessage());
                    innerEx = innerEx.InnerException;
                }
            }

            CoutWriteLine();
            CoutWriteLine("Obtaining Workflow Type Name...");
            CoutWriteLine();

            string workflowTypeName = await workflow2.GetWorkflowTypeNameAsync();

            CoutWriteLine($"Obtained. {nameof(workflowTypeName)}={workflowTypeName.QuoteOrNull()}.");
            CoutWriteLine("Updated handle info:");
            CoutWriteLine($"    IsBound:         {workflow2.IsBound}");
            CoutWriteLine($"    WorkflowChainId: {workflow2.WorkflowChainId}");

            CoutWriteLine();
            CoutWriteLine("Sending signal to a workflow...");
            CoutWriteLine();

            await workflow.SignalAsync("Some-Signal-01", "Some-Signal-Argument");

            CoutWriteLine("Signal sent. Look for it in the workflow history.");

            CoutWriteLine();
            CoutWriteLine("Sending query to a workflow...");

            try
            {
                TimeSpan delayQueryCancel = TimeSpan.FromSeconds(2);
                CoutWriteLine($"The workflow was not designed for queries,"
                              + $" so we will cancel the wait for query completion after '{delayQueryCancel}'.");
                CoutWriteLine();

                using CancellationTokenSource query01CancelControl = new(delayQueryCancel);
                object resQuery01 = await workflow.QueryAsync<object, string>("Some-Query-01",
                    "Some-Query-Argument",
                    cancelToken: query01CancelControl.Token);

                CoutWriteLine("Query sent. Look for it in the workflow history.");
                CoutWriteLine($"Query result: |{Format.QuoteIfString(resQuery01)}|.");
            }
            catch (OperationCanceledException opCncldEx)
            {
                CoutWriteLine("Received expected exception.");
                CoutWriteLine(opCncldEx.TypeAndMessage());

                Exception innerEx = opCncldEx.InnerException;
                while (innerEx != null)
                {
                    CoutWriteLine("\n  Inner --> " + innerEx.TypeAndMessage());
                    innerEx = innerEx.InnerException;
                }
            }

            CoutWriteLine();
            CoutWriteLine("Sending cancellation request to a workflow...");
            CoutWriteLine();

            await workflow.RequestCancellationAsync();

            CoutWriteLine("Cancellation requested. However, it the workflow was not designed to honor it, so it will remain ignored. Look for the request in the workflow history.");
            CoutWriteLine("Look for the request in the workflow history.");

            CoutWriteLine();
            CoutWriteLine("Getting LatestRun of the workflow...");

            IWorkflowRunHandle latestRun = await workflow.GetLatestRunAsync();

            CoutWriteLine("Got the latest run. Calling Getting LatestRun of the workflow...");
            CoutWriteLine($"    Namespace:       {latestRun.Namespace}");
            CoutWriteLine($"    WorkflowId:      {latestRun.WorkflowId}");
            CoutWriteLine($"    WorkflowRunId:   {latestRun.WorkflowRunId}");

            CoutWriteLine();
            CoutWriteLine("Sending signal to a run...");
            CoutWriteLine();

            await latestRun.SignalAsync("Some-Signal-02", Payload.Unnamed(42, DateTimeOffset.Now, "Last-Signal-Argument-Value"));

            CoutWriteLine("Signal sent to run. Look for it in the run history.");

            CoutWriteLine();
            CoutWriteLine("Creating an independent workflow run handle...");

            IWorkflowRunHandle run2 = client.CreateWorkflowRunHandle(latestRun.WorkflowId, latestRun.WorkflowRunId);

            CoutWriteLine("Independent run handle created.");
            CoutWriteLine($"    Namespace:       {latestRun.Namespace}");
            CoutWriteLine($"    WorkflowId:      {latestRun.WorkflowId}");
            CoutWriteLine($"    WorkflowRunId:   {latestRun.WorkflowRunId}");

            CoutWriteLine();
            CoutWriteLine("Obtaining owner workflow of independent run handle...");
            CoutWriteLine();

            IWorkflowHandle ownerWorkflow = await run2.GetOwnerWorkflowAsync();

            CoutWriteLine("Owner workflow obtained.");
            CoutWriteLine($"    Namespace:       {ownerWorkflow.Namespace}");
            CoutWriteLine($"    WorkflowId:      {ownerWorkflow.WorkflowId}");
            CoutWriteLine($"    IsBound:         {ownerWorkflow.IsBound}");
            CoutWriteLine($"    WorkflowChainId: {ownerWorkflow.WorkflowChainId}");

            CoutWriteLine();
            CoutWriteLine($"Owner-workflow-handle and the Original-workflow refer to the same chain (TRUE expected):"
                          + $" {ownerWorkflow.WorkflowChainId.Equals(workflow.WorkflowChainId)}");

            CoutWriteLine($"Owner-workflow-handle and the Original-workflow are the same instance (FALSE expected):"
                          + $" {Object.ReferenceEquals(ownerWorkflow, workflow)}");

            CoutWriteLine();
            CoutWriteLine("Sending four signals to a run using independent handle...");
            CoutWriteLine();

            object[] signal3Inputvalues = new object[] { 42, new { Custom = "Foo", Datatype = 18 } };
            await latestRun.SignalAsync("Some-Signal-03a", Payload.Unnamed(signal3Inputvalues));
            await latestRun.SignalAsync("Some-Signal-03b", Payload.Unnamed<object[]>(signal3Inputvalues));
            await latestRun.SignalAsync("Some-Signal-03c", Payload.Unnamed(42, 43, 44));
            await latestRun.SignalAsync("Some-Signal-03d", Payload.Unnamed<int[]>(new[] { 42, 43, 44 }));

            CoutWriteLine("Signasl sent via intependent run handle. Look for them in the run history.");

            _ = Task.Run(async () =>
            {
                TimeSpan delayTermination = TimeSpan.FromSeconds(2);
                CoutWriteLine($"Started automatic termination invoker with a delay of '{delayTermination}'.");

                await Task.Delay(delayTermination);
                CoutWriteLine($"Delay of {delayTermination} elapsed. Terminating workflow...");

                await workflow.TerminateAsync("Good-reason-for-termination", details: DateTimeOffset.Now);
                CoutWriteLine($"Workflow terminated.");
            });

            CoutWriteLine();
            CoutWriteLine("Waiting for result...");
            CoutWriteLine();

            try
            {
                await workflow.GetResultAsync<object>();

                throw new Exception("ERROR. We should never get here, because the above code is expected to throw.");
            }
            catch (WorkflowConcludedAbnormallyException wcaEx) when (wcaEx.ConclusionStatus == WorkflowExecutionStatus.Terminated)
            {
                CoutWriteLine("Received expected exception.");
                CoutWriteLine(wcaEx.TypeAndMessage());

                Exception innerEx = wcaEx.InnerException;
                while (innerEx != null)
                {
                    CoutWriteLine("\n  Inner --> " + innerEx.TypeAndMessage());
                    innerEx = innerEx.InnerException;
                }
            }

            CoutWriteLine();
            CoutWriteLine("Creating a handle to a non-existing workflow...");

            IWorkflowHandle workflow3 = client.CreateWorkflowHandle("Non-Existing-Workflow-Id");

            CoutWriteLine("Created. Info:");
            CoutWriteLine($"    Namespace:       {workflow3.Namespace}");
            CoutWriteLine($"    WorkflowId:      {workflow3.WorkflowId}");
            CoutWriteLine($"    IsBound:         {workflow3.IsBound}");

            CoutWriteLine();
            CoutWriteLine("Verifying existence...");
            CoutWriteLine();

            bool wfExists = await workflow3.ExistsAsync();

            CoutWriteLine($"Verified. {nameof(wfExists)}={wfExists}.");
            CoutWriteLine("Updated handle info:");
            CoutWriteLine($"    IsBound:         {workflow3.IsBound}");

            try
            {
                CoutWriteLine($"    WorkflowChainId: {workflow3.WorkflowChainId}");

                throw new Exception("ERROR. We should never get here, because the above code is expected to throw.");
            }
            catch (InvalidOperationException invOpEx)
            {
                CoutWriteLine($"    Expected exception while getting {nameof(workflow3.WorkflowChainId)}:");
                CoutWriteLine($"    --> {invOpEx.TypeAndMessage()}");

                Exception innerEx = invOpEx.InnerException;
                while (innerEx != null)
                {
                    CoutWriteLine("\n      Inner --> " + invOpEx.TypeAndMessage());
                    innerEx = innerEx.InnerException;
                }
            }

            CoutWriteLine();
            CoutWriteLine("Sending signal to a non-existing workflow...");
            CoutWriteLine();

            try
            {
                await workflow3.SignalAsync("Some-Signal-04");

                throw new Exception("ERROR. We should never get here, because the above code is expected to throw.");
            }
            catch (WorkflowNotFoundException wnfEx)
            {
                CoutWriteLine("Received expected exception.");
                CoutWriteLine(wnfEx.TypeAndMessage());

                Exception innerEx = wnfEx.InnerException;
                while (innerEx != null)
                {
                    CoutWriteLine("\n  Inner --> " + innerEx.TypeAndMessage());
                    innerEx = innerEx.InnerException;
                }
            }

            CoutWriteLine();
            CoutWriteLine("Waiting for result of a non-existing workflow...");
            CoutWriteLine();

            try
            {
                await workflow3.GetResultAsync<object>();

                throw new Exception("ERROR. We should never get here, because the above code is expected to throw.");
            }
            catch (WorkflowNotFoundException wnfEx)
            {
                CoutWriteLine("Received expected exception.");
                CoutWriteLine(wnfEx.TypeAndMessage());

                Exception innerEx = wnfEx.InnerException;
                while (innerEx != null)
                {
                    CoutWriteLine("\n  Inner --> " + innerEx.TypeAndMessage());
                    innerEx = innerEx.InnerException;
                }
            }

            CoutWriteLine();
            CoutWriteLine($"E2E Scenario complete ({nameof(SimpleClientInvocationsE2ETest)}).");
            CoutWriteLine();
        }

        private async Task<ITemporalClient> InitTemporalClient()
        {
            TemporalClient client = CreateTemporalClient();
            await client.EnsureConnectedAsync();
            return client;
        }
    }
}