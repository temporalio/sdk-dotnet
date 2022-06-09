using System;
using System.Threading.Tasks;

using Temporal.Api.Enums.V1;
using Temporal.Util;
using Temporal.WorkflowClient;

namespace Temporal.Sdk.WorkflowClient.UsageSamples
{
    /// <summary>
    /// In this Part we consider similarities and differences between 2 key concepts in Temporal:
    /// "Workflow Chain" and "Workflow Run".
    /// 
    /// Workflow Chain:
    /// Technically, a Temporal workflow is a chain-sequence of one or more Workflow Runs from the initial
    /// invocation of a workflow to its eventual conclusion. Thus, a logical workflow (as in "just" workflow,
    /// or "the" workflow) is really a Workflow Chain.           
    ///
    /// Workflow Run:
    /// A single execution of the main workflow routine.
    /// Completely executing a workflow from the initial invocation to the eventual conclusion involves a chain
    /// of one or more Workflow Runs.
    ///
    /// The Temporal server orchestrates the execution of workflows by ensuring that Workers execute Workflow
    /// Runs as required to complete a Workflow Chain (aka a logical Workflow).
    /// Within the Client SDK:
    ///  * Workflow Chains are modeled by instances of type `IWorkflowHandle`.
    ///  * Workflow Runs are modeled by instances of type `IWorkflowRunHandle`.
    ///
    /// In most scenarios, users only need to interact with logical workflows, i.e., with Workflow Chains
    /// via the respective `IWorkflowHandle` instances. For example, when a user needs to send a signal to
    /// a workflow (or perform a query, terminate, cancel, etc...) they invoke a corresponding
    /// `IWorkflowHandle` API. Under the covers, the handle automatically interacts with the current Run
    /// within the Workflow Chain represented by the handle. Similarly, when a users polls for the result
    /// of a workflow, the respective `IWorkflowHandle` API automatically "follows" the chain until the
    /// "final" run of a chain completes.
    /// However, in some advanced scenarios users need to explicitly interact with a specific Run within the 
    /// Workflow Chain representing a particular logical workflow. This is done using a `IWorkflowRunHandle`
    /// instances. Examples in this file demonstrate how to do that.
    /// </summary>
    /// <remarks>This class contains usage sample intended for education and API review. It may not actually execute
    /// successfully because IDs used in these samples may not actually exist on the backing Temporal server.</remarks>
    public class Part3_AddressIndividualRuns
    {
        public void Run()
        {
            Console.WriteLine($"\n{this.GetType().Name}.{nameof(Run)}(..) started.\n");

            RunAsync().GetAwaiter().GetResult();

            Console.WriteLine();
            Console.WriteLine($"\n{this.GetType().Name}.{nameof(Run)}(..) finished.\nPress Enter.");
            Console.ReadLine();
        }

        public async Task RunAsync()
        {
            Console.WriteLine($"\n{this.GetType().Name}.{nameof(RunAsync)}(..) started.\n");

            Console.WriteLine($"\nExecuting {nameof(ObtainRunHandleAsync)}...");
            await ObtainRunHandleAsync();

            Console.WriteLine($"\nExecuting {nameof(GetWorkflowRunResultAsync)}...");
            await GetWorkflowRunResultAsync();

            Console.WriteLine($"\nExecuting {nameof(AddressSpecialRunWithinChainAsync)}...");
            await AddressSpecialRunWithinChainAsync();

            Console.WriteLine($"\nExecuting {nameof(AddressSpecificRunWithinChainAsync)}...");
            await AddressSpecificRunWithinChainAsync();

            Console.WriteLine($"\nExecuting {nameof(ListRunsWithinChainAsync)}...");
            await ListRunsWithinChainAsync();

            Console.WriteLine($"\nExecuting {nameof(InteractWithRunAsync)}...");
            await InteractWithRunAsync();

            Console.WriteLine($"\nExecuting {nameof(GetResultNoLongPollAsync)}...");
            await GetResultNoLongPollAsync();

            Console.WriteLine($"\nExecuting {nameof(NavigateAcrossChainsAndRunsAsync)}...");
            await NavigateAcrossChainsAndRunsAsync();

            Console.WriteLine($"\n{this.GetType().Name}.{nameof(RunAsync)}(..) finished.\n");
        }

        public async Task ObtainRunHandleAsync()
        {
            ITemporalClient client = ObtainClient();

            // Get the current status of the logical workflow with the workflow-id "Some-Workflow-Id" by
            // automatically addressing the current Run within the respective Workflow Chain:
            {
                IWorkflowHandle workflow = client.CreateWorkflowHandle("Some-Workflow-Id");
                WorkflowExecutionStatus wfStatus = await workflow.GetStatusAsync();

                Console.WriteLine($"The current Execution Status of the Workflow Chain with the workflow-id \"{workflow.WorkflowId}\" is:");
                Console.WriteLine($"    '{wfStatus}' (={(int) wfStatus})");
            }

            // Get the current status of the Workflow Run with the workflow-run-id "Some-Workflow-Run-Id"
            // within a Workflow Chain with the workflow-id "Some-Workflow-Id" by explicitly addressing
            // the respective Workflow Run:            
            {
                IWorkflowRunHandle workflowRun = client.CreateWorkflowRunHandle("Some-Workflow-Id", "Some-Workflow-Run-Id");
                WorkflowExecutionStatus runStatus = await workflowRun.GetStatusAsync();

                Console.WriteLine($"The current Execution Status of the Workflow Run with the workflow-id \"{workflowRun.WorkflowId}\""
                                + $" and the workflow-run-id \"{workflowRun.WorkflowRunId}\" is:");
                Console.WriteLine($"    '{runStatus}' (={(int) runStatus})");
            }
        }

        public async Task GetWorkflowRunResultAsync()
        {
            ITemporalClient client = ObtainClient();

            // The `IWorkflowHandle.GetResultAsync<T>(..)` method will poll the server until it obtains the result
            // of the FINAL run within the workflow chain. If the current run within the chain is NOT the final run
            // (e.g., it is continued-as-new), then the polling will "flow" over to the subsequent runs within the
            // chain until the FINAL run eventually concludes:
            {
                IWorkflowHandle workflow = client.CreateWorkflowHandle("Some-Workflow-Id");
                int resultValue = await workflow.GetResultAsync<int>();

                Console.WriteLine($"The final result value of the Workflow Chain with the workflow-id \"{workflow.WorkflowId}\" is:");
                Console.WriteLine($"    `{resultValue}`");
            }

            // On contrary, the `IWorkflowRunHandle.GetResultAsync<T>(..)` method will poll the server only until
            // it obtains the result of the specific run represented by the IWorkflowRunHandle instance. If the
            // workflow chain is continued with subsequent runs, this method will not "flow" with the chain:            
            {
                IWorkflowRunHandle workflowRun = client.CreateWorkflowRunHandle("Some-Workflow-Id", "Some-Workflow-Run-Id");
                int resultValue = await workflowRun.GetResultAsync<int>();

                Console.WriteLine($"The result value of the Workflow Run with the workflow-id \"{workflowRun.WorkflowId}\""
                                + $" and the workflow-run-id \"{workflowRun.WorkflowRunId}\" is:");
                Console.WriteLine($"    `{resultValue}`");
            }
        }

        public async Task AddressSpecialRunWithinChainAsync()
        {
            // Technically, a Temporal workflow is a Chain of one or more Workflow Runs. Such a Workflow Chain spans
            // from the initial invocation of a workflow to its eventual conclusion. A Chain may include many Runs,
            // however, some of the runs have a special significance:
            //  - The FIRST Run of a Chain is the Run that was initiated when the logical workflow was started.
            //    Typically, the first Run is used to IDENTIFY the entire chain.
            //    I.e., the workflow-run-id of the first Run within a Workflow Chain identifies not only that
            //    particular Run, but also the Workflow Chain as a whole.
            //  - The LATEST Run of a Chain is the Run that has started most recently. Such LATEST Run is sometimes
            //    also termed CURRENT. However, technically, the latest run may have a `Running` status (then the
            //    entire Chain is also said to be Running) or it may already have concluded (then the entire Chain
            //    is said to be concluded). To account for this, the .NET Workflow Client SDK uses the term LATEST
            //    rather than CURRENT.
            //  - The FINAL Run of a chain is the Run that concludes the chain. No further Runs are known to follow
            //    after the final run of a given Workflow Chain. In most scenarios it is not known with what Status
            //    a Run will conclude, until it does so. Therefore, a workflow may not have a final Run until it
            //    eventually concludes.
            //
            // The examples below show how to obtain and use handles to specific Runs within a given workflow:

            ITemporalClient client = ObtainClient();
            IWorkflowHandle workflow = client.CreateWorkflowHandle("Some-Workflow-Id");

            // Address the FIRST Run within a Workflow Chain:
            {
                IWorkflowRunHandle run = await workflow.GetFirstRunAsync();
                WorkflowExecutionStatus status = await run.GetStatusAsync();

                Console.WriteLine($"Properties of the FIRST run of the workflow with the workflow-id \"{workflow.WorkflowId}\":");
                Console.WriteLine($"    Namespace:       \"{run.Namespace}\"");
                Console.WriteLine($"    Workflow-Id:     \"{run.WorkflowId}\"");
                Console.WriteLine($"    Workflow-Run-Id: \"{run.WorkflowId}\"");
                Console.WriteLine($"    Status:          '{status}' (={(int) status})");
            }

            // Address the LATEST Run within a Workflow Chain:
            {
                IWorkflowRunHandle run = await workflow.GetLatestRunAsync();
                WorkflowExecutionStatus status = await run.GetStatusAsync();

                Console.WriteLine($"Properties of the LATEST run of the workflow with the workflow-id \"{workflow.WorkflowId}\":");
                Console.WriteLine($"    Namespace:       \"{run.Namespace}\"");
                Console.WriteLine($"    Workflow-Id:     \"{run.WorkflowId}\"");
                Console.WriteLine($"    Workflow-Run-Id: \"{run.WorkflowId}\"");
                Console.WriteLine($"    Status:          '{status}' (={(int) status})");
            }

            // Address the FINAL Run within a Workflow Chain, IFF the final Run is available:
            {
                if ((await workflow.TryGetFinalRunAsync()).IsSuccess(out IWorkflowRunHandle run))
                {
                    WorkflowExecutionStatus status = await run.GetStatusAsync();

                    Console.WriteLine($"Properties of the FINAL run of the workflow with the workflow-id \"{workflow.WorkflowId}\":");
                    Console.WriteLine($"    Namespace:       \"{run.Namespace}\"");
                    Console.WriteLine($"    Workflow-Id:     \"{run.WorkflowId}\"");
                    Console.WriteLine($"    Workflow-Run-Id: \"{run.WorkflowId}\"");
                    Console.WriteLine($"    Status:          '{status}' (={(int) status})");
                }
                else
                {
                    WorkflowExecutionStatus chainStatus = await workflow.GetStatusAsync();

                    Console.WriteLine($"The FINAL run is not avaiable for the workflow with the workflow-id \"{workflow.WorkflowId}\".");
                    Console.WriteLine($"    Status of Workflow Chain: '{chainStatus}' (={(int) chainStatus})");
                }
            }
        }

        public async Task AddressSpecificRunWithinChainAsync()
        {
            // On top of accessing a SPECIAL Run within a Workflow Chain (i.e. First/Latest/Final Run), users may
            // also address ANY Run within a Workflow Chain by specifying the respective id.

            ITemporalClient client = ObtainClient();
            IWorkflowHandle workflow = client.CreateWorkflowHandle("Some-Workflow-Id");

            // Check whether a Run with a specific workflow-run-id exists within a given workflow:
            {
                const string CheckRunId = "Some-Workflow-Run-Id-A";
                bool runExists = await workflow.CreateRunHandle(CheckRunId).ExistsAsync();

                Console.WriteLine($"A Workflow Run with a workflow-run-id \"{CheckRunId}\""
                                + (runExists ? " exists" : "does not exist")
                                + $" within a Chain with the workflow-id \"{workflow.WorkflowId}\".");
            }

            // Address the Run with a specific workflow-run-id within a Workflow Chain:
            {
                IWorkflowRunHandle run = workflow.CreateRunHandle("Some-Workflow-Run-Id");
                WorkflowExecutionStatus status = await run.GetStatusAsync();

                Console.WriteLine($"Properties of the SPECIFIED run within the workflow with the workflow-id \"{workflow.WorkflowId}\":");
                Console.WriteLine($"    Namespace:       \"{run.Namespace}\"");
                Console.WriteLine($"    Workflow-Id:     \"{run.WorkflowId}\"");
                Console.WriteLine($"    Workflow-Run-Id: \"{run.WorkflowId}\"");
                Console.WriteLine($"    Status:          '{status}' (={(int) status})");
            }
        }

        public Task ListRunsWithinChainAsync()
        {
            // @ToDo: API to list Runs within a given chain needs to be added. Likely not in the first SDK version.
            return Task.FromResult(0);
        }

        public async Task InteractWithRunAsync()
        {
            // `IWorkflowRunHandle` has many methods that behave similar to the respective `IWorkflowHandle` methods
            // with the difference that they address the specific run represented by the `IWorkflowRunHandle`, rather
            // than the latest Run within a chain.

            IWorkflowRunHandle wfRun = await ObtainWorkflowRunAsync();

            // Signal:
            {
                double signalArg = 42.0;
                await wfRun.SignalAsync("Signal-Name", signalArg);
            }

            // Query
            {
                DateTimeOffset queryArg = DateTimeOffset.Now;
                decimal queryResultValue = await wfRun.QueryAsync<DateTimeOffset, decimal>("Query-Name", queryArg);

                Console.WriteLine($"{nameof(queryResultValue)}: {queryResultValue}.");
            }

            // Request Cancellation:
            {
                await wfRun.RequestCancellationAsync();
            }


            // Describe:
            {
                string taskQueue = (await wfRun.DescribeAsync()).ExecutionConfig.TaskQueue.Name;

                Console.WriteLine($"{nameof(taskQueue)}: {taskQueue}.");
            }

            // Terminate:
            {
                await wfRun.TerminateAsync();
            }

            // Await conclusion:
            {
                IWorkflowRunResult wfResultInfo = await wfRun.AwaitConclusionAsync();

                Console.WriteLine($"The Workflow Run concluded:");
                Console.WriteLine($"    Namespace:         \"{wfResultInfo.Namespace}\".");
                Console.WriteLine($"    WorkflowId:        \"{wfResultInfo.WorkflowId}\".");
                Console.WriteLine($"    WorkflowRunId:     \"{wfResultInfo.WorkflowRunId}\".");
                Console.WriteLine($"    Conclusion Status: \"{wfResultInfo.Status}\".");
                Console.WriteLine($"    Failure:           {(wfResultInfo.Failure == null ? "None" : wfResultInfo.Failure.TypeAndMessage())}.");
            }
        }

        public async Task GetResultNoLongPollAsync()
        {
            // As described earlier, the methods `IWorkflowHandle.GetResultAsync<T>(..)` and `IWorkflowRunHandle.GetResultAsync<T>(..)`
            // both perform long polls and complete only when the result is available. Moreover, the `IWorkflowHandle` variant
            // also follows the Workflow CHain until its eventual conclusion.
            // Users can make use of the APIs mentioned in previous examples to get the result of a logical workflow without performing
            // a long poll IFF such result is already available.

            IWorkflowHandle workflow = await ObtainWorkflowAsync();

            int? resultValue = null;

            if ((await workflow.TryGetFinalRunAsync()).IsSuccess(out IWorkflowRunHandle wfRun))
            {
                resultValue = await wfRun.GetResultAsync<int>();
            }

            if (resultValue.HasValue)
            {
                Console.WriteLine($"The final result is available and the value is `{resultValue.Value}`.");
            }
            else
            {
                Console.WriteLine($"The final result is not yet available.");
            }
        }

        public async Task NavigateAcrossChainsAndRunsAsync()
        {
            // Previous examples demnstrated how to obtain Runs withn a Workflow Chain.
            // It is also possible to navigate from a Run back to the Chain that contains it.
            // In this example, we start with an arbitrary Workflow Run. We aim to obtain the FIRST Run of the Chain
            // that contains the specified Run. For that, we first obtain the Chain, and then the first Run.

            IWorkflowRunHandle specifiedRun = await ObtainWorkflowRunAsync();

            IWorkflowHandle workflow = await specifiedRun.GetOwnerWorkflowAsync();
            IWorkflowRunHandle firstRun = await workflow.GetFirstRunAsync();

            Console.WriteLine($"Workflow-Run-Id of {nameof(specifiedRun)}: \"{specifiedRun.WorkflowRunId}\"");
            Console.WriteLine($"Workflow-Run-Id of {nameof(firstRun)}:     \"{firstRun.WorkflowRunId}\"");
        }

        #region --- Helpers ---

        private void UseWorkflow(IWorkflowHandle workflow)
        {
            Validate.NotNull(workflow);
            // Do stuff with `workflow`.
        }

        private ITemporalClient ObtainClient()
        {
            TemporalClientConfiguration clinetConfig = TemporalClientConfiguration.ForLocalHost();
            return new TemporalClient(clinetConfig);
        }

        private string CreateUniqueWorkflowId()
        {
            return $"Sample-Workflow-Id-{Guid.NewGuid().ToString("D")}";
        }

        private async Task<IWorkflowHandle> ObtainWorkflowAsync()
        {
            ITemporalClient client = ObtainClient();

            string workflowId = CreateUniqueWorkflowId();
            IWorkflowHandle workflow = await client.StartWorkflowAsync(workflowId,
                                                                       "Sample-Workflow-Type-Name",
                                                                       "Sample-Task-Queue");

            return workflow;
        }

        private async Task<IWorkflowRunHandle> ObtainWorkflowRunAsync()
        {
            IWorkflowHandle workflow = await ObtainWorkflowAsync();
            return await workflow.GetLatestRunAsync();
        }

        #endregion --- Helpers ---
    }
}
