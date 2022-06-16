using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Temporal.Util;

using Temporal.Common;
using IUnnamedPayloadContainer = Temporal.Common.Payloads.PayloadContainers.IUnnamed;
using INamedPayloadContainer = Temporal.Common.Payloads2.PayloadContainers.INamed;  // For Demo only. `...Payloads2...` will be just `...Payloads...` later.
using PayloadContainer = Temporal.Common.Payloads2.Payload;
using Temporal.Worker.Hosting;

namespace Temporal.Sdk.ActivityWorker.UsageSamples
{
    /// <summary>
    /// ToDo: Things not YET covered in this spec:
    /// 
    /// * Worker options / worker creation settings:
    ///   - Connection options
    ///   - TLS
    ///   - Client
    ///   - Worker level options (Temporal behavior)
    ///   
    /// * Interceptors
    /// 
    /// * Serialzation config
    /// 
    /// * Fatal error handling
    /// 
    /// ----------- ----------- ----------- ----------- -----------
    /// 
    /// Note about invoking activities from workflows:
    /// 
    /// Invoking activities from workflows is explicitly NOT in scope of this spec.
    /// Here, I outline a very high level approach just to provide context.
    /// The details of this will NOT be dicussed while reviewing the activity implementation SDK scope.
    /// 
    /// Unlike Java, the workflow implementation will NOT use ifaces to invoke activities.
    /// Instead, we will generate classes using source generators
    /// [e.g. see https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview,
    /// https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md].
    /// 
    /// The user will need to write something like:
    /// ```cs
    /// [TemporalActivityStub(ImplementationMethod = Namespace.Type.Method)]
    /// internal partial class MyActivity : IActivityStub
    /// {
    /// }
    /// ```
    /// 
    /// For this, `Namespace.Type.Method` must be in an accessible assembly.
    /// We will generate the "rest" of `MyActivity` and add it to the project.
    /// 
    /// An alternative will be a name/string based API similar to what is already implemented for the workflow client.
    /// </summary>
    public class Program
    {
        public static void Main(string[] _)
        {
            Console.WriteLine($"RuntimeEnvironmentInfo: \n{RuntimeEnvironmentInfo.SingletonInstance}");

            Console.WriteLine($"\n{typeof(Program).FullName} has finished.\n");
        }

        /// <summary>
        /// Used in samples to pass data around.
        /// </summary>
        internal class NameData
        {
            public string Name { get; }
            public NameData(string name) { Name = name; }
            public override string ToString() { return Name ?? ""; }
        }

        /// <summary>
        /// Minimal code to host an activity worker.
        /// </summary>
        private static class Sample01_MinimalActivityHost
        {
            public static void SayHello()
            {
                Console.WriteLine("Hello World!");
            }

            public static async Task ExecuteActivityHostAsync()
            {
                // Create a new worker:
                using TemporalActivityWorker worker = new();

                // Add an activity:
                worker.RegisterActivity("Say-Hello", (_) => SayHello());

                // Start the worker (the returned task is completed when the worker has started):
                await worker.StartAsync();

                Console.WriteLine($"{nameof(Sample01_MinimalActivityHost)}: Worker started. Press enter to terminate.");
                Console.ReadLine();

                // Terminate the worker (the returned task is completed when the worker has finished shutting down):
                await worker.TerminateAsync();
            }
        }

        /// <summary>
        /// Any existing of new method can become an activity. Just pass it to the worker.
        /// There are signature restrictions. Later samples show how to create wrappers/adapters to overcome those.
        /// This example uses static activity methods.
        /// </summary>
        private static class Sample02_HostSimpleSyncActivities
        {
            public static void SayHello()
            {
                Console.WriteLine("Hello!");
            }

            public static void SayHelloWithMetadata(IWorkflowActivityContext activityCtx)
            {
                string reqWfId = activityCtx.RequestingWorkflow.WorkflowId;
                string reqWfRunId = activityCtx.RequestingWorkflow.RunId;

                Console.WriteLine($"Hello! (Requested by Wf:[Id='{reqWfId}'; RunId='{reqWfRunId}'])");
            }

            public static void SayHelloName(NameData name)
            {
                Console.WriteLine($"Hello, {name}!");
            }

            public static void SayHelloNameWithMetadata(NameData name, IWorkflowActivityContext activityCtx)
            {
                string reqWfId = activityCtx.RequestingWorkflow.WorkflowId;
                string reqWfRunId = activityCtx.RequestingWorkflow.RunId;

                Console.WriteLine($"Hello, {name}! (Requested by Wf:[Id='{reqWfId}'; RunId='{reqWfRunId}'])");
            }

            public static string EnterWord()
            {
                Console.Write($"Enter a word >");
                return Console.ReadLine();
            }

            public static string EnterWordWithMetadata(IWorkflowActivityContext activityCtx)
            {
                Console.Write($"Enter a word (WfType:'{activityCtx.RequestingWorkflow.TypeName}') >");
                return Console.ReadLine();
            }

            public static string EnterWordWithName(NameData name)
            {
                Console.Write($"Hi, {name}, enter a word >");
                return Console.ReadLine();
            }

            public static string EnterWordWithNameAndMetadata(NameData name, IWorkflowActivityContext activityCtx)
            {
                Console.Write($"Hi, {name}, enter a word (WfType:'{activityCtx.RequestingWorkflow.TypeName}') >");
                return Console.ReadLine();
            }

            public static async Task ExecuteActivityHostAsync()
            {
                using TemporalActivityWorker worker = new();

                worker.RegisterActivity("Say-Hello", SayHello)
                      .RegisterActivity("Say-Hello2", SayHelloWithMetadata)

                      .RegisterActivity<NameData>("Say-Name", SayHelloName)
                      .RegisterActivity<NameData>("Say-Name2", SayHelloNameWithMetadata)

                      .RegisterActivity("Enter-Word", EnterWord)
                      .RegisterActivity("Enter-Word2", EnterWordWithMetadata)

                      .RegisterActivity<NameData, string>("Enter-Word-Name", EnterWordWithName)
                      .RegisterActivity<NameData, string>("Enter-Word-Name2", EnterWordWithNameAndMetadata);

                await worker.StartAsync();

                Console.WriteLine($"{nameof(Sample02_HostSimpleSyncActivities)}: Worker started. Press enter to terminate.");
                Console.ReadLine();

                await worker.TerminateAsync();
            }

            /// <summary>
            /// Async activities are just as simple as sync activities.
            /// This sample shows how to use adapters if there is an existing method that uses
            /// a CancellationToken directly and is not aware of IWorkflowActivityContext.
            /// </summary>
            private static class Sample03_HostSimpleAsyncActivities
            {
                public static async Task WriteHelloAsync()
                {
                    using StreamWriter writer = new("DemoFile.txt");
                    await writer.WriteLineAsync("Hello!");
                }

                public static async Task WriteHelloWithCancelAsync(CancellationToken cancelToken)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    using StreamWriter writer = new("DemoFile.txt");
                    await writer.WriteLineAsync($"Hello!");
                }

                public static async Task WriteHelloWithMetadataAsync(IWorkflowActivityContext activityCtx)
                {
                    activityCtx.CancelToken.ThrowIfCancellationRequested();

                    string reqWfId = activityCtx.RequestingWorkflow.WorkflowId;
                    string reqWfRunId = activityCtx.RequestingWorkflow.RunId;

                    using StreamWriter writer = new("DemoFile.txt");
                    await writer.WriteLineAsync($"Hello! (Requested by Wf:[Id='{reqWfId}'; RunId='{reqWfRunId}'])");
                }

                public static async Task WriteHelloNameAsync(NameData name)
                {
                    using StreamWriter writer = new("DemoFile.txt");
                    await writer.WriteLineAsync($"Hello {name}!");
                }

                public static async Task WriteHelloNameWithCancelAsync(NameData name, CancellationToken cancelToken)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    using StreamWriter writer = new("DemoFile.txt");
                    await writer.WriteLineAsync($"Hello {name}!");
                }

                public static async Task WriteHelloNameWithMetadataAsync(NameData name, IWorkflowActivityContext activityCtx)
                {
                    activityCtx.CancelToken.ThrowIfCancellationRequested();

                    string reqWfId = activityCtx.RequestingWorkflow.WorkflowId;
                    string reqWfRunId = activityCtx.RequestingWorkflow.RunId;

                    using StreamWriter writer = new("DemoFile.txt");
                    await writer.WriteLineAsync($"Hello {name}! (Requested by Wf:[Id='{reqWfId}'; RunId='{reqWfRunId}'])");
                }

                public static async Task<string> ReadWordAsync()
                {
                    using StreamReader reader = new("DemoFile.txt");
                    return await reader.ReadLineAsync();
                }

                public static async Task<string> ReadWordWithCancelAsync(CancellationToken cancelToken)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    using StreamReader reader = new("DemoFile.txt");
                    return await reader.ReadLineAsync();
                }

                public static async Task<string> ReadWordWithMetadataAsync(IWorkflowActivityContext activityCtx)
                {
                    activityCtx.CancelToken.ThrowIfCancellationRequested();

                    using StreamReader reader = new("DemoFile.txt");
                    string line = await reader.ReadLineAsync();

                    return $"{line} (read on behalf of:'{activityCtx.RequestingWorkflow.TypeName}')";
                }

                public static async Task<string> ReadWordWithNameAsync(NameData name)
                {
                    using StreamReader reader = new("DemoFile.txt");
                    string line = await reader.ReadLineAsync();

                    return $"{line} (read for '{name}')";
                }

                public static async Task<string> ReadWordWithNameAndCancelAsync(NameData name, CancellationToken cancelToken)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    using StreamReader reader = new("DemoFile.txt");
                    string line = await reader.ReadLineAsync();

                    return $"{line} (read for '{name}')";
                }

                public static async Task<string> ReadWordWithNameAndMetadataAsync(NameData name, IWorkflowActivityContext activityCtx)
                {
                    activityCtx.CancelToken.ThrowIfCancellationRequested();

                    using StreamReader reader = new("DemoFile.txt");
                    string line = await reader.ReadLineAsync();

                    return $"{line} (read for '{name}' on behalf of:'{activityCtx.RequestingWorkflow.TypeName}')";
                }

                public static async Task ExecuteActivityHostAsync()
                {
                    using TemporalActivityWorker worker = new();

                    worker.RegisterActivity("Write-Hello", WriteHelloAsync)
                          .RegisterActivity("Write-Hello2", (ctx) => WriteHelloWithCancelAsync(ctx.CancelToken))
                          .RegisterActivity("Write-Hello3", WriteHelloWithMetadataAsync)

                          .RegisterActivity<NameData>("Write-Name", WriteHelloNameAsync)
                          .RegisterActivity<NameData>("Write-Name2", (nm, ctx) => WriteHelloNameWithCancelAsync(nm, ctx.CancelToken))
                          .RegisterActivity<NameData>("Write-Name3", WriteHelloNameWithMetadataAsync)

                          .RegisterActivity("Read-Word", ReadWordAsync)
                          .RegisterActivity("Read-Word2", (ctx) => ReadWordWithCancelAsync(ctx.CancelToken))
                          .RegisterActivity("Read-Word3", ReadWordWithMetadataAsync)

                          .RegisterActivity<NameData, string>("Read-Word-Name", ReadWordWithNameAsync)
                          .RegisterActivity<NameData, string>("Read-Word-Name2", (nm, ctx) => ReadWordWithNameAndCancelAsync(nm, ctx.CancelToken))
                          .RegisterActivity<NameData, string>("Read-Word-Name3", ReadWordWithNameAndMetadataAsync);

                    await worker.StartAsync();

                    Console.WriteLine($"{nameof(Sample03_HostSimpleAsyncActivities)}: Worker started. Press enter to terminate.");
                    Console.ReadLine();

                    await worker.TerminateAsync();
                }
            }
        }

        /// <summary>
        /// Previous examples show static methods representing activities.
        /// An activity may also be implemented as a method on a class instance, such that the instance is shared
        /// across activity invocations. In such scenarios, make sure that activity invocations may happen concurrently,
        /// and synchronize as appropriate.
        /// </summary>
        private static class Sample04_NonStaticActivityMethods
        {
            public sealed class CustomLogger : IDisposable
            {
                private readonly SemaphoreSlim _lock = new(initialCount: 1);
                private readonly StreamWriter _writer;

                public CustomLogger(string fileName)
                {
                    _writer = new StreamWriter(fileName);
                }

                public void Dispose()
                {
                    DisposeAsync().GetAwaiter().GetResult();
                }

                public async Task DisposeAsync()
                {
                    await _lock.WaitAsync();

                    try
                    {
                        _writer.Dispose();
                    }
                    finally
                    {
                        _lock.Release();
                    }
                }

                public async Task WriteLogLineAsync(string logLevel, string logMessage)
                {
                    await _lock.WaitAsync();

                    try
                    {
                        await _writer.WriteLineAsync($"[{logLevel}] {logMessage}");
                    }
                    finally
                    {
                        _lock.Release();
                    }
                }

                public Task WriteErrorLineAsync(string logMessage)
                {
                    return WriteLogLineAsync("ERR", logMessage);
                }

                public Task WriteInfoLineAsync(string logMessage)
                {
                    return WriteLogLineAsync("INF", logMessage);
                }
            }

            public static async void ConfigureActivityHost()
            {
                using CustomLogger logger = new("DemoLog.txt");

                TemporalActivityWorker worker = new();

                worker.RegisterActivity<string>("Log-Error", logger.WriteErrorLineAsync)
                      .RegisterActivity<string>("Log-Info", logger.WriteInfoLineAsync);

                await worker.StartAsync();

                Console.WriteLine($"{nameof(Sample04_NonStaticActivityMethods)}: Worker started. Press enter to terminate.");
                Console.ReadLine();

                await worker.TerminateAsync();
            }
        }

        /// <summary>
        /// Previous example shows how an instance method can implements an activity while sharing the underlying
        /// class instance across activity invocations. This example shows how a new instance can be created for each
        /// activity invocation.        
        /// </summary>
        private static class Sample05_NewClassInstanceForEachActivityInvocation
        {
            public sealed class CustomLogger2 : IDisposable
            {
                private readonly StreamWriter _writer;

                public CustomLogger2(string fileName)
                {
                    _writer = new StreamWriter(fileName);
                }

                public void Dispose()
                {
                    _writer.Dispose();
                }

                public Task WriteLogLineAsync(string logLevel, string logMessage)
                {
                    return _writer.WriteLineAsync($"[{logLevel}] {logMessage}");
                }

                public Task WriteErrorLineAsync(string logMessage)
                {
                    return WriteLogLineAsync("ERR", logMessage);
                }

                public Task WriteInfoLineAsync(string logMessage)
                {
                    return WriteLogLineAsync("INF", logMessage);
                }
            }

            private static int s_logFileIndex = 0;

            public static async void ConfigureActivityHost()
            {
                using TemporalActivityWorker worker = new();

                worker.RegisterActivity<string>("Log-Error", (msg) => (new CustomLogger2($"DemoLog-{Interlocked.Increment(ref s_logFileIndex)}.txt")).WriteErrorLineAsync(msg))
                      .RegisterActivity<string>("Log-Info", (msg) => (new CustomLogger2($"DemoLog-{Interlocked.Increment(ref s_logFileIndex)}.txt")).WriteInfoLineAsync(msg));

                await worker.StartAsync();

                Console.WriteLine($"{nameof(Sample05_NewClassInstanceForEachActivityInvocation)}: Worker started. Press enter to terminate.");
                Console.ReadLine();

                await worker.TerminateAsync();
            }
        }

        /// <summary>
        /// Previous examples use a single business logic argument.
        /// This example demonstrates an adapter for multiple arguments.
        /// </summary>
        private static class Sample06_MultipleArguments
        {
            public static Task<double> ComputeStuffAsync(int x, int y, int z)
            {
                double d = Math.Sqrt(x * x + y * y + z * z);
                return Task.FromResult(d);
            }

            public static async void ConfigureActivityHost()
            {

                using TemporalActivityWorker worker = new();

                // .NET Workflows will be encouraged to use named arguments.
                // (Single argument-orject with names properties)
                // The transport mechanism is a single payload carrying a json object with named properties.

                worker.RegisterActivity<INamedPayloadContainer, double>("ComputeStuff",
                                                                        (inp) => ComputeStuffAsync(inp.GetValue<int>("x"),
                                                                                                   inp.GetValue<int>("y"),
                                                                                                   inp.GetValue<int>("z")));

                // .NET Workflows will be encouraged to use named arguments.
                // (Single argument-orject with names properties)
                // If a workflow is nevertheless using multiple payload entries to send multiple arguments (other lang?),
                // an "unnamed" container can be used to receive them. However, the "named" approch (above) is preferred.

                worker.RegisterActivity<IUnnamedPayloadContainer, double>("ComputeStuff2",
                                                                          (inp) => ComputeStuffAsync(inp.GetValue<int>(0),
                                                                                                     inp.GetValue<int>(1),
                                                                                                     inp.GetValue<int>(2)));

                await worker.StartAsync();

                Console.WriteLine($"{nameof(Sample06_MultipleArguments)}: Worker started. Press enter to terminate.");
                Console.ReadLine();

                await worker.TerminateAsync();
            }
        }

        /// <summary>
        /// This sample shows the underlying mechanics of activity hosting. 
        /// Registering the delegate-based activities demonstrated earlier results in automaic creation of an activity factory that
        /// is actually registered with the worker. For each activity invocation requested by the server, the factory creates a simple
        /// wrapper activity implementation that actually call through to the user-specified delegate.
        /// Users can choose to implement such factories themselves.
        /// There are several advanced scenarios where this may be beneficial. E.g.:
        ///   - Accociate an activity type name with the implementation rather than the registration.
        ///   - Scan an assembly and auto-register all activities in it by registering all respective factories.
        ///   - Easier dependency injection of activity components.
        ///   - ...
        /// This example shows a very simple activity registered in a few different ways (see comments below).
        /// </summary>
        private static class Sample07_ActivityFactories
        {
            public class SayHelloActivity : IActivityImplementation<IPayload.Void, IPayload.Void>
            {
                public Task<IPayload.Void> ExecuteAsync(IPayload.Void _, IWorkflowActivityContext __)
                {
                    Console.WriteLine("Hello World!");
                    return IPayload.Void.CompletedTask;
                }
            }

            public class SayHelloActivityFactory : AutoInstantiatingActivityImplementationFactory<SayHelloActivity, IPayload.Void, IPayload.Void>
            {
            }

            public class SayHelloWorldActivityFactory : InstanceSharingActivityImplementationFactory<SayHelloActivity, IPayload.Void, IPayload.Void>
            {
                public SayHelloWorldActivityFactory(SayHelloActivity activity) : base(activityTypeName: "SayHelloWorld", sharedActivityImplementation: activity) { }
            }

            public static async void ConfigureActivityHost()
            {
                // The SAME activity instance will be used for every invocation:
                // (the activity-type-name will be "SayHelloWorld": it is explicitly specified in the implementation of `SayHelloWorldActivityFactory`)

                using TemporalActivityWorker worker1 = new();

                SayHelloActivity reusedActivity = new();
                SayHelloWorldActivityFactory factory1 = new(reusedActivity);

                worker1.RegisterActivity(factory1);

                // A NEW activity instance will be used for every invocation:

                using TemporalActivityWorker worker2 = new();

                worker2.RegisterActivity(new SayHelloActivityFactory());            // (the activity-type-name will be "SayHello": it is extracted from the type name
                                                                                    // `SayHelloActivity` which is a type parameter passed from SayHelloActivityFactory to its base)

                worker2.RegisterActivity("SayHi", new SayHelloActivityFactory());   // (the activity-type-name will be "SayHi": the default providd by the factory is overwritted
                                                                                    // during registration)

                await Task.WhenAll(worker1.StartAsync(),
                                   worker2.StartAsync());

                Console.WriteLine($"{nameof(Sample07_ActivityFactories)}: Workers started. Press enter to terminate.");
                Console.ReadLine();

                await Task.WhenAll(worker1.TerminateAsync(),
                                   worker2.TerminateAsync());
            }
        }

        /// <summary>
        /// This example also uses an explicit activity implementation instead of a delegate-based implementation.
        /// It shows how an actual business logic implementation can be adapred to the recommented pattern of always
        /// using named payloads, without the overhead of creating new data transport types.
        /// Note: we highly encourage users to use single argument-object with named properties for both, input and return
        /// types. However, people still may need to use activity implementations that do not follow this guidance.
        /// This example demonstrates an adapter pattern.
        /// 
        /// Note that in this example the activity implementation instance can be reused becasue it does not have local state.
        /// </summary>
        private static class Sample08_AdaptingMultipleArgsToNamedPayloads
        {
            public class CalculateDistanceActivity : IActivityImplementation<INamedPayloadContainer, INamedPayloadContainer>
            {
                public class Factory : InstanceSharingActivityImplementationFactory<CalculateDistanceActivity, INamedPayloadContainer, INamedPayloadContainer>
                {
                    public Factory() : base(new CalculateDistanceActivity()) { }
                }

                public Task<INamedPayloadContainer> ExecuteAsync(INamedPayloadContainer input, IWorkflowActivityContext activityCtx)
                {
                    double dist = CalculateDistance(input.GetValue<double>("X1"), input.GetValue<double>("Y1"),
                                                    input.GetValue<double>("X2"), input.GetValue<double>("Y2"));

                    return Task.FromResult(PayloadContainer.Named("Distance", dist));
                }

                public double CalculateDistance(double x1, double y1, double x2, double y2)
                {
                    double dX = (x2 - x1);
                    double dY = (y2 - y1);
                    return Math.Sqrt(dX * dX + dY * dY);
                }
            }

            public static async void ConfigureActivityHost()
            {
                using TemporalActivityWorker worker = new();

                worker.RegisterActivity(new CalculateDistanceActivity.Factory());

                await worker.StartAsync();

                Console.WriteLine($"{nameof(Sample08_AdaptingMultipleArgsToNamedPayloads)}: Worker started. Press enter to terminate.");
                Console.ReadLine();

                await worker.TerminateAsync();
            }
        }

        /// <summary>
        /// For the heartbeat API we choose a name that EXPLICITLY communicates that the heartbeat
        /// may or may not be delivered immediately. This is consistent with the "request cancellation" pattern.
        /// </summary>
        private static class Sample09_Heartbeat
        {
            internal static bool IsPrime(int n, List<int> prevPrimes)
            {
                int sqrtN = (int) Math.Sqrt(n);

                int c = prevPrimes.Count;
                for (int i = 1; i < c; i++)
                {
                    int m = prevPrimes[i];
                    if (m > sqrtN)
                    {
                        break;
                    }

                    if (n % m == 0)
                    {
                        return false;
                    }
                }

                return true;
            }

            public static IList<int> CalculatePrimes(int max, IWorkflowActivityContext activityCtx)
            {
                List<int> primes = new();

                for (int n = 1; n <= max; n++)
                {
                    activityCtx.CancelToken.ThrowIfCancellationRequested();  // Check for cancellation

                    if (IsPrime(n, primes))
                    {
                        primes.Add(n);
                    }

                    activityCtx.RequestRecordHeartbeat(n);  // Attempt (?) Record heartbeat
                }

                return primes;
            }

            public static async void ConfigureActivityHost()
            {
                using TemporalActivityWorker worker = new();

                worker.RegisterActivity<int, IList<int>>("CalculatePrimeNumbers", CalculatePrimes);

                await worker.StartAsync();

                Console.WriteLine($"{nameof(Sample09_Heartbeat)}: Worker started. Press enter to terminate.");
                Console.ReadLine();

                await worker.TerminateAsync();
            }
        }

        /// <summary>
        /// This example shows how to execute an activity on the thread pool, and how to use another heartbeat overload.
        /// </summary>
        private static class Sample10_ParallelThreading
        {
            public static IList<int> CalculatePrimes(int max, IWorkflowActivityContext activityCtx)
            {
                List<int> primes = new();

                for (int n = 1; n <= max; n++)
                {
                    if (activityCtx.CancelToken.IsCancellationRequested)
                    {
                        // Cancellation is cooperative. Activity may not react to it. 
                        // In this sample, we DO react to it, but for the server the activity will appear completed,
                        // becasue we did not throw the OperationCanceledException like in the previous examples.

                        break;
                    }

                    if (Sample09_Heartbeat.IsPrime(n, primes))
                    {
                        primes.Add(n);
                    }

                    // In previous examples we passed arguments to `RequestHeartbeatRecording`.
                    // However, that is optional:
                    activityCtx.RequestRecordHeartbeat();
                }

                return primes;
            }

            public static async void ConfigureActivityHost()
            {
                using TemporalActivityWorker worker = new();

                // Invoke the action on the thread pool (the action is complete when the task compeltes):
                worker.RegisterActivity<int, IList<int>>("CalculatePrimeNumbers", async (n, ctx) => await Task.Run(() => CalculatePrimes(n, ctx)));

                await worker.StartAsync();

                Console.WriteLine($"{nameof(Sample10_ParallelThreading)}: Worker started. Press enter to terminate.");
                Console.ReadLine();

                await worker.TerminateAsync();
            }
        }

        /// <summary>
        /// The `IWorkflowActivityContext` is an explicit. This has some advantages:
        ///   * Unit testing for activities is simpler (the context can be easily mocked).
        ///   * Supporting activity-functionality is an explicit API contract
        ///     (i.e. a library method that can heartbeat can demonstrate it by having IWorkflowActivityContext in the signature).
        ///   * If an application / implemetation wishes to promote the activity context to be implicitly available, it can choose
        ///     whatever mechanism it wants. The Temporal SDK does not want to be prescriptive on that regard.
        /// We demonstrate 2 approaches to making the activity context implicitly available: (a) Instance field and (b) Async local.
        /// This sample demonstrates the first of the two.
        /// </summary>
        private static class Sample11_WorkflowActivityContextA
        {
            internal class DoSomethingActivity
            {
                private readonly IWorkflowActivityContext _activityCtx;

                public DoSomethingActivity(IWorkflowActivityContext activityCtx)
                {
                    _activityCtx = activityCtx;
                }

                public async Task ExecuteAsync(string dataFile)
                {
                    object data = await ReadInputAsync(dataFile);
                    ProcessData(data);
                    await WriteOutputAsync(data, dataFile);
                }

                private async Task<object> ReadInputAsync(string dataFile)
                {
                    // Note the usage of `_activityCtx` in the method body:

                    List<byte> data = new();
                    byte[] buff = new byte[1024 * 20];
                    using FileStream fs = File.OpenRead(dataFile);

                    int readBytes = await fs.ReadAsync(buff, 0, buff.Length, _activityCtx.CancelToken);
                    while (readBytes > 0)
                    {
                        for (int i = 0; i < readBytes; i++)
                        {
                            data.Add(buff[i]);
                        }

                        _activityCtx.RequestRecordHeartbeat();
                        readBytes = await fs.ReadAsync(buff, 0, buff.Length, _activityCtx.CancelToken);
                    }

                    return data;
                }

                [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Sample")]
                private void ProcessData(object data)
                {
                    // . . .
                }

                [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Sample")]
                private Task WriteOutputAsync(object data, string dataFile)
                {
                    // . . .
                    return null;
                }
            }

            public static async void ConfigureActivityHost()
            {
                using TemporalActivityWorker worker = new();

                // Invoke the action on the thread pool (the action is complete when the task compeltes):
                worker.RegisterActivity<string>("Do-Something", (datFl, ctx) => (new DoSomethingActivity(ctx)).ExecuteAsync(datFl));

                await worker.StartAsync();

                Console.WriteLine($"{nameof(Sample11_WorkflowActivityContextA)}: Worker started. Press enter to terminate.");
                Console.ReadLine();

                await worker.TerminateAsync();
            }
        }

        /// <summary>
        /// We demonstrate 2 approaches to making the activity context (`IWorkflowActivityContext`) implicitly available:
        /// (a) Instance field and (b) Async local.
        /// The previous example demonstrated the first.
        /// This sample demonstrates the second.
        /// The business logic shown here matched the previous example.
        /// </summary>
        private static class Sample12_WorkflowActivityContextB
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Sample")]
            internal static class WorkflowActivityContexts
            {
                private static class CtxContainers
                {
                    internal static readonly AsyncLocal<IWorkflowActivityContext> s_doSomething = new();
                    // More actvities...
                }

                public static IWorkflowActivityContext DoSomething
                {
                    get { return CtxContainers.s_doSomething.Value; }
                    set { CtxContainers.s_doSomething.Value = value; }
                }

                // More actvities...
            }

            public static async Task DoSomethingAsync(string dataFile, IWorkflowActivityContext activityCtx)
            {
                WorkflowActivityContexts.DoSomething = activityCtx;

                try
                {
                    object data = await ReadInputAsync(dataFile);
                    ProcessData(data);
                    await WriteOutputAsync(data, dataFile);
                }
                finally
                {
                    WorkflowActivityContexts.DoSomething = null;
                }
            }

            private static async Task<object> ReadInputAsync(string dataFile)
            {
                // Note the usage of `WorkflowActivityContexts.DoSomething` in the method body:

                List<byte> data = new();
                byte[] buff = new byte[1024 * 20];
                using FileStream fs = File.OpenRead(dataFile);

                int readBytes = await fs.ReadAsync(buff, 0, buff.Length, WorkflowActivityContexts.DoSomething.CancelToken);
                while (readBytes > 0)
                {
                    for (int i = 0; i < readBytes; i++)
                    {
                        data.Add(buff[i]);
                    }

                    WorkflowActivityContexts.DoSomething.RequestRecordHeartbeat();
                    readBytes = await fs.ReadAsync(buff, 0, buff.Length, WorkflowActivityContexts.DoSomething.CancelToken);
                }

                return data;
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Sample")]
            private static void ProcessData(object data)
            {
                // . . .
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Sample")]
            private static Task WriteOutputAsync(object data, string dataFile)
            {
                // . . .
                return null;
            }

            public static async void ConfigureActivityHost()
            {
                using TemporalActivityWorker worker = new();

                // Invoke the action on the thread pool (the action is complete when the task compeltes):
                worker.RegisterActivity<string>("Do-Something", DoSomethingAsync);

                await worker.StartAsync();

                Console.WriteLine($"{nameof(Sample12_WorkflowActivityContextB)}: Worker started. Press enter to terminate.");
                Console.ReadLine();

                await worker.TerminateAsync();
            }
        }
    }
}