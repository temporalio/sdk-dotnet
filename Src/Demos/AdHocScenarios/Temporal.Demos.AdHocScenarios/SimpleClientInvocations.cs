using System;
using System.Threading;
using System.Threading.Tasks;

using Temporal.Api.Enums.V1;
using Temporal.Common;
using Temporal.Util;
using Temporal.WorkflowClient;
using Temporal.WorkflowClient.Errors;

namespace Temporal.Demos.AdHocScenarios
{
    internal class SimpleClientInvocations
    {
        // Mock Change
        public void Run()
        {
            Console.WriteLine();

            RunAsync().GetAwaiter().GetResult();

            Console.WriteLine();
            Console.WriteLine("Done. Press Enter.");
            Console.ReadLine();
        }

        private static class CreateClient
        {
            public static Task<ITemporalClient> LocalPlainAsync()
            {
                Console.WriteLine("Creating a client over a plain (unsecured) channel...");

                return TemporalClient.ConnectAsync(TemporalClientConfiguration.ForLocalHost());
            }

            public static Task<ITemporalClient> LocalWithCustomCaAsync()
            {
                Console.WriteLine("Creating a client over an SSL channel with a custom server CA...");

                return TemporalClient.ConnectAsync(TemporalClientConfiguration.ForLocalHost() with
                {
                    ServiceConnection = new TemporalClientConfiguration.Connection() with
                    {
                        IsTlsEnabled = true,
                        ServerCertAuthority = TemporalClientConfiguration.TlsCertificate.FromPemFile(
                                        @"PATH\ca.cert")
                    }

                });
            }

            public static Task<ITemporalClient> LocalWithNoServerValidation()
            {
                if (RuntimeEnvironmentInfo.SingletonInstance.CoreAssembyInfo.IsMscorlib)
                {
                    throw new PlatformNotSupportedException("Disabling server certificate validation is not supported"
                                                          + " on classic Net Fx becasue of limitations in the .NET gRPC library used"
                                                          + " with those Framework versions. Use a custom Cert Authority identity of a"
                                                          + " modern .NET version.");
                }

                Console.WriteLine("Creating a client over an SSL channel with no server sert validation...");

                return TemporalClient.ConnectAsync(TemporalClientConfiguration.ForLocalHost() with
                {
                    ServiceConnection = new TemporalClientConfiguration.Connection() with
                    {
                        IsTlsEnabled = true,
                        SkipServerCertValidation = true
                    }
                });
            }

            public static Task<ITemporalClient> TemporalCloud()
            {
                Console.WriteLine("Creating a client over an SSL channel to Temporal Cloud...");

                return TemporalClient.ConnectAsync(TemporalClientConfiguration.ForTemporalCloud(
                                        "NAMESPACE",
                                        @"PATH\NAME.crt.pem",
                                        @"PATH\NAME.key.pem"
                                        ));
            }
        }

        public async Task RunAsync()
        {
            ITemporalClient client = await CreateClient.LocalPlainAsync();
            //ITemporalClient client = await CreateClient.LocalWithCustomCaAsync();
            //ITemporalClient client = await CreateClient.LocalWithNoServerValidation();
            //ITemporalClient client = await CreateClient.TemporalCloud();

            string demoWfId = "Demo Workflow XYZ / " + Format.AsReadablePreciseLocal(DateTimeOffset.Now);

            Console.WriteLine("Starting a workflow...");
            IWorkflowHandle workflow = await client.StartWorkflowAsync(demoWfId,
                                                                      "DemoWorkflowTypeName",
                                                                      "DemoTaskQueue");
            Console.WriteLine("Started. Info:");
            Console.WriteLine($"    Namespace:       {workflow.Namespace}");
            Console.WriteLine($"    WorkflowId:      {workflow.WorkflowId}");
            Console.WriteLine($"    IsBound:         {workflow.IsBound}");
            Console.WriteLine($"    WorkflowChainId: {workflow.WorkflowChainId}");

            Console.WriteLine();
            Console.WriteLine("Attempting to start a workflow with the same WorkflowId...");
            Console.WriteLine();

            try
            {
                await client.StartWorkflowAsync(demoWfId, "DemoWorkflowTypeName", "DemoTaskQueue");

                throw new Exception("ERROR. We should never get here, because the above code is expected to throw.");
            }
            catch (WorkflowAlreadyExistsException waeEx)
            {
                Console.WriteLine("Received expected exception.");
                Console.WriteLine(waeEx.TypeAndMessage());

                Exception innerEx = waeEx.InnerException;
                while (innerEx != null)
                {
                    Console.WriteLine("\n  Inner --> " + innerEx.TypeAndMessage());
                    innerEx = innerEx.InnerException;
                }
            }

            Console.WriteLine();
            Console.WriteLine("Creating a handle to the existing workflow...");
            Console.WriteLine();

            IWorkflowHandle workflow2 = client.CreateWorkflowHandle(demoWfId);

            Console.WriteLine("Created. Info:");
            Console.WriteLine($"    Namespace:       {workflow2.Namespace}");
            Console.WriteLine($"    WorkflowId:      {workflow2.WorkflowId}");
            Console.WriteLine($"    IsBound:         {workflow2.IsBound}");

            try
            {
                Console.WriteLine($"    WorkflowChainId: {workflow2.WorkflowChainId}");

                throw new Exception("ERROR. We should never get here, because the above code is expected to throw.");
            }
            catch (InvalidOperationException invOpEx)
            {
                Console.WriteLine($"    Expected exception while getting {nameof(workflow2.WorkflowChainId)}:");
                Console.WriteLine($"    --> {invOpEx.TypeAndMessage()}");

                Exception innerEx = invOpEx.InnerException;
                while (innerEx != null)
                {
                    Console.WriteLine("\n      Inner --> " + invOpEx.TypeAndMessage());
                    innerEx = innerEx.InnerException;
                }
            }

            Console.WriteLine();
            Console.WriteLine("Obtaining Workflow Type Name...");
            Console.WriteLine();

            string workflowTypeName = await workflow2.GetWorkflowTypeNameAsync();

            Console.WriteLine($"Obtained. {nameof(workflowTypeName)}={workflowTypeName.QuoteOrNull()}.");
            Console.WriteLine("Updated handle info:");
            Console.WriteLine($"    IsBound:         {workflow2.IsBound}");
            Console.WriteLine($"    WorkflowChainId: {workflow2.WorkflowChainId}");

            Console.WriteLine();
            Console.WriteLine("Sending signal to a workflow...");
            Console.WriteLine();

            await workflow.SignalAsync("Some-Signal-01", "Some-Signal-Argument");

            Console.WriteLine("Signal sent. Look for it in the workflow history.");

            Console.WriteLine();
            Console.WriteLine("Sending query to a workflow...");

            try
            {
                TimeSpan delayQueryCancel = TimeSpan.FromSeconds(2);
                Console.WriteLine($"The workflow was not designed for queries,"
                                + $" so we will cancel the wait for query completion after '{delayQueryCancel}'.");
                Console.WriteLine();

                using CancellationTokenSource query01CancelControl = new(delayQueryCancel);
                object resQuery01 = await workflow.QueryAsync<object, string>("Some-Query-01",
                                                                              "Some-Query-Argument",
                                                                              cancelToken: query01CancelControl.Token);

                Console.WriteLine("Query sent. Look for it in the workflow history.");
                Console.WriteLine($"Query result: |{Format.QuoteIfString(resQuery01)}|.");
            }
            catch (OperationCanceledException opCncldEx)
            {
                Console.WriteLine("Received expected exception.");
                Console.WriteLine(opCncldEx.TypeAndMessage());

                Exception innerEx = opCncldEx.InnerException;
                while (innerEx != null)
                {
                    Console.WriteLine("\n  Inner --> " + innerEx.TypeAndMessage());
                    innerEx = innerEx.InnerException;
                }
            }

            Console.WriteLine();
            Console.WriteLine("Sending cancellation request to a workflow...");
            Console.WriteLine();

            await workflow.RequestCancellationAsync();

            Console.WriteLine("Cancellation requested. However, it the workflow was not designed to honor it, so it will remain ignored. Look for the request in the workflow history.");
            Console.WriteLine("Look for the request in the workflow history.");

            Console.WriteLine();
            Console.WriteLine("Getting LatestRun of the workflow...");

            IWorkflowRunHandle latestRun = await workflow.GetLatestRunAsync();

            Console.WriteLine("Got the latest run. Calling Getting LatestRun of the workflow...");
            Console.WriteLine($"    Namespace:       {latestRun.Namespace}");
            Console.WriteLine($"    WorkflowId:      {latestRun.WorkflowId}");
            Console.WriteLine($"    WorkflowRunId:   {latestRun.WorkflowRunId}");

            Console.WriteLine();
            Console.WriteLine("Sending signal to a run...");
            Console.WriteLine();

            await latestRun.SignalAsync("Some-Signal-02", Payload.Unnamed(42, DateTimeOffset.Now, "Last-Signal-Argument-Value"));

            Console.WriteLine("Signal sent to run. Look for it in the run history.");

            Console.WriteLine();
            Console.WriteLine("Creating an independent workflow run handle...");

            IWorkflowRunHandle run2 = client.CreateWorkflowRunHandle(latestRun.WorkflowId, latestRun.WorkflowRunId);

            Console.WriteLine("Independent run handle created.");
            Console.WriteLine($"    Namespace:       {latestRun.Namespace}");
            Console.WriteLine($"    WorkflowId:      {latestRun.WorkflowId}");
            Console.WriteLine($"    WorkflowRunId:   {latestRun.WorkflowRunId}");

            Console.WriteLine();
            Console.WriteLine("Obtaining owner workflow of independent run handle...");
            Console.WriteLine();

            IWorkflowHandle ownerWorkflow = await run2.GetOwnerWorkflowAsync();

            Console.WriteLine("Owner workflow obtained.");
            Console.WriteLine($"    Namespace:       {ownerWorkflow.Namespace}");
            Console.WriteLine($"    WorkflowId:      {ownerWorkflow.WorkflowId}");
            Console.WriteLine($"    IsBound:         {ownerWorkflow.IsBound}");
            Console.WriteLine($"    WorkflowChainId: {ownerWorkflow.WorkflowChainId}");

            Console.WriteLine();
            Console.WriteLine($"Owner-workflow-handle and the Original-workflow refer to the same chain (TRUE expected):"
                            + $" {ownerWorkflow.WorkflowChainId.Equals(workflow.WorkflowChainId)}");

            Console.WriteLine($"Owner-workflow-handle and the Original-workflow are the same instance (FALSE expected):"
                            + $" {Object.ReferenceEquals(ownerWorkflow, workflow)}");

            Console.WriteLine();
            Console.WriteLine("Sending four signals to a run using independent handle...");
            Console.WriteLine();

            object[] signal3Inputvalues = new object[] { 42, new { Custom = "Foo", Datatype = 18 } };
            await latestRun.SignalAsync("Some-Signal-03a", Payload.Unnamed(signal3Inputvalues));
            await latestRun.SignalAsync("Some-Signal-03b", Payload.Unnamed<object[]>(signal3Inputvalues));
            await latestRun.SignalAsync("Some-Signal-03c", Payload.Unnamed(42, 43, 44));
            await latestRun.SignalAsync("Some-Signal-03d", Payload.Unnamed<int[]>(new[] { 42, 43, 44 }));

            Console.WriteLine("Signasl sent via intependent run handle. Look for them in the run history.");

            _ = Task.Run(async () =>
                {
                    TimeSpan delayTermination = TimeSpan.FromSeconds(2);
                    Console.WriteLine($"Started automatic termination invoker with a delay of '{delayTermination}'.");

                    await Task.Delay(delayTermination);
                    Console.WriteLine($"Delay of {delayTermination} elapsed. Terminating workflow...");

                    await workflow.TerminateAsync("Good-reason-for-termination", details: DateTimeOffset.Now);
                    Console.WriteLine($"Workflow terminated.");
                });

            Console.WriteLine();
            Console.WriteLine("Waiting for result...");
            Console.WriteLine();

            try
            {
                await workflow.GetResultAsync<object>();

                throw new Exception("ERROR. We should never get here, because the above code is expected to throw.");
            }
            catch (WorkflowConcludedAbnormallyException wcaEx) when (wcaEx.ConclusionStatus == WorkflowExecutionStatus.Terminated)
            {
                Console.WriteLine("Received expected exception.");
                Console.WriteLine(wcaEx.TypeAndMessage());

                Exception innerEx = wcaEx.InnerException;
                while (innerEx != null)
                {
                    Console.WriteLine("\n  Inner --> " + innerEx.TypeAndMessage());
                    innerEx = innerEx.InnerException;
                }
            }

            Console.WriteLine();
            Console.WriteLine("Creating a handle to a non-existing workflow...");

            IWorkflowHandle workflow3 = client.CreateWorkflowHandle("Non-Existing-Workflow-Id");

            Console.WriteLine("Created. Info:");
            Console.WriteLine($"    Namespace:       {workflow3.Namespace}");
            Console.WriteLine($"    WorkflowId:      {workflow3.WorkflowId}");
            Console.WriteLine($"    IsBound:         {workflow3.IsBound}");

            Console.WriteLine();
            Console.WriteLine("Verifying existence...");
            Console.WriteLine();

            bool wfExists = await workflow3.ExistsAsync();

            Console.WriteLine($"Verified. {nameof(wfExists)}={wfExists}.");
            Console.WriteLine("Updated handle info:");
            Console.WriteLine($"    IsBound:         {workflow3.IsBound}");

            try
            {
                Console.WriteLine($"    WorkflowChainId: {workflow3.WorkflowChainId}");

                throw new Exception("ERROR. We should never get here, because the above code is expected to throw.");
            }
            catch (InvalidOperationException invOpEx)
            {
                Console.WriteLine($"    Expected exception while getting {nameof(workflow3.WorkflowChainId)}:");
                Console.WriteLine($"    --> {invOpEx.TypeAndMessage()}");

                Exception innerEx = invOpEx.InnerException;
                while (innerEx != null)
                {
                    Console.WriteLine("\n      Inner --> " + invOpEx.TypeAndMessage());
                    innerEx = innerEx.InnerException;
                }
            }

            Console.WriteLine();
            Console.WriteLine("Sending signal to a non-existing workflow...");
            Console.WriteLine();

            try
            {
                await workflow3.SignalAsync("Some-Signal-04");

                throw new Exception("ERROR. We should never get here, because the above code is expected to throw.");
            }
            catch (WorkflowNotFoundException wnfEx)
            {
                Console.WriteLine("Received expected exception.");
                Console.WriteLine(wnfEx.TypeAndMessage());

                Exception innerEx = wnfEx.InnerException;
                while (innerEx != null)
                {
                    Console.WriteLine("\n  Inner --> " + innerEx.TypeAndMessage());
                    innerEx = innerEx.InnerException;
                }
            }

            Console.WriteLine();
            Console.WriteLine("Waiting for result of a non-existing workflow...");
            Console.WriteLine();

            try
            {
                await workflow3.GetResultAsync<object>();

                throw new Exception("ERROR. We should never get here, because the above code is expected to throw.");
            }
            catch (WorkflowNotFoundException wnfEx)
            {
                Console.WriteLine("Received expected exception.");
                Console.WriteLine(wnfEx.TypeAndMessage());

                Exception innerEx = wnfEx.InnerException;
                while (innerEx != null)
                {
                    Console.WriteLine("\n  Inner --> " + innerEx.TypeAndMessage());
                    innerEx = innerEx.InnerException;
                }
            }
        }
    }
}
