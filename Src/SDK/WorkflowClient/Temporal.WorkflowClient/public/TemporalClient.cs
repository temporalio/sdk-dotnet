using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Temporal.Common;
using Temporal.Serialization;
using Temporal.Util;
using Temporal.WorkflowClient.Interceptors;
using Temporal.WorkflowClient.OperationConfigurations;

namespace Temporal.WorkflowClient
{
    public sealed class TemporalClient : ITemporalClient
    {
        #region -- Static APIs --

        private static int s_identityMarkersCount = 0;
        private static string s_processDescriptor = null;

        /// <summary>
        /// </summary>
        /// <remarks></remarks>
        public static async Task<ITemporalClient> ConnectAsync(TemporalClientConfiguration config, CancellationToken cancelToken = default)
        {
            TemporalClient client = new(config);
            await client.EnsureConnectedAsync(cancelToken);
            return client;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static string CreateProcessDescriptor()
        {
            try
            {
                CurrentProcess.GetIdentityInfo(out string processName, out string machineName, out int processId);
                return $"{machineName}/{processName}/{processId}";
            }
            catch
            {
                return Guid.NewGuid().ToString("D");
            }
        }

        private static string GetProcessDescriptor()
        {
            string processDescriptor = s_processDescriptor;
            if (processDescriptor == null)
            {
                try
                {
                    processDescriptor = CreateProcessDescriptor();
                }
                catch { }

                if (processDescriptor == null)
                {
                    processDescriptor = Guid.NewGuid().ToString("D");
                }

                s_processDescriptor = processDescriptor;  // benign race
            }

            return processDescriptor;
        }

        private static string CreateIdentityMarker()
        {
            int identityMarkersIndex;
            unchecked
            {
                identityMarkersIndex = Interlocked.Increment(ref s_identityMarkersCount);
            }

            return $"{GetProcessDescriptor()}/{identityMarkersIndex}";
        }

        #endregion -- Static APIs --


        #region -- Fields, Ctors, Common properties --

        private WorkflowServiceClientEnvelope _grpcServiceClientEnvelope = null;
        private readonly string _identityMarker;

        public TemporalClient()
            : this(TemporalClientConfiguration.ForLocalHost())
        {
        }

        public TemporalClient(TemporalClientConfiguration config)
        {
            TemporalClientConfiguration.Validate(config);

            Configuration = config;
            _identityMarker = config.ClientIdentityMarker ?? CreateIdentityMarker();
        }

        public TemporalClientConfiguration Configuration { get; }

        public string Namespace { get { return Configuration.Namespace; } }

        #endregion -- Fields, Ctors, Common properties --


        #region -- Workflow access and control APIs --

        #region StartWorkflowAsync(..)

        public Task<IWorkflowHandle> StartWorkflowAsync(string workflowId,
                                                        string workflowTypeName,
                                                        string taskQueue,
                                                        StartWorkflowConfiguration workflowConfig = null,
                                                        CancellationToken cancelToken = default)
        {
            return StartWorkflowAsync<IPayload.Void>(workflowId, workflowTypeName, taskQueue, Payload.Void, workflowConfig, cancelToken);
        }

        public async Task<IWorkflowHandle> StartWorkflowAsync<TWfArg>(string workflowId,
                                                                      string workflowTypeName,
                                                                      string taskQueue,
                                                                      TWfArg workflowArg,
                                                                      StartWorkflowConfiguration workflowConfig = null,
                                                                      CancellationToken cancelToken = default)
        {
            IWorkflowHandle workflow = CreateWorkflowHandle(workflowId);
            await workflow.StartAsync<TWfArg>(workflowTypeName,
                                              taskQueue,
                                              workflowArg,
                                              workflowConfig,
                                              throwIfWorkflowChainAlreadyExists: true,
                                              cancelToken);
            return workflow;
        }

        #endregion StartWorkflowAsync(..)


        #region SignalWorkflowWithStartAsync(..)

        public Task<IWorkflowHandle> SignalWorkflowWithStartAsync(string workflowId,
                                                                 string workflowTypeName,
                                                                 string taskQueue,
                                                                 string signalName,
                                                                 StartWorkflowConfiguration workflowConfig = null,
                                                                 CancellationToken cancelToken = default)
        {
            return SignalWorkflowWithStartAsync<IPayload.Void, IPayload.Void>(workflowId,
                                                                              workflowTypeName,
                                                                              taskQueue,
                                                                              workflowArg: Payload.Void,
                                                                              signalName,
                                                                              signalArg: Payload.Void,
                                                                              workflowConfig,
                                                                              cancelToken);
        }

        public Task<IWorkflowHandle> SignalWorkflowWithStartAsync<TSigArg>(string workflowId,
                                                                          string workflowTypeName,
                                                                          string taskQueue,
                                                                          string signalName,
                                                                          TSigArg signalArg,
                                                                          StartWorkflowConfiguration workflowConfig = null,
                                                                          CancellationToken cancelToken = default)
        {
            return SignalWorkflowWithStartAsync<IPayload.Void, TSigArg>(workflowId,
                                                                        workflowTypeName,
                                                                        taskQueue,
                                                                        workflowArg: Payload.Void,
                                                                        signalName,
                                                                        signalArg,
                                                                        workflowConfig,
                                                                        cancelToken);
        }

        public Task<IWorkflowHandle> SignalWorkflowWithStartAsync<TWfArg>(string workflowId,
                                                                         string workflowTypeName,
                                                                         string taskQueue,
                                                                         TWfArg workflowArg,
                                                                         string signalName,
                                                                         StartWorkflowConfiguration workflowConfig = null,
                                                                         CancellationToken cancelToken = default)
        {
            return SignalWorkflowWithStartAsync<TWfArg, IPayload.Void>(workflowId,
                                                                       workflowTypeName,
                                                                       taskQueue,
                                                                       workflowArg,
                                                                       signalName,
                                                                       signalArg: Payload.Void,
                                                                       workflowConfig,
                                                                       cancelToken);
        }

        public async Task<IWorkflowHandle> SignalWorkflowWithStartAsync<TWfArg, TSigArg>(string workflowId,
                                                                                         string workflowTypeName,
                                                                                         string taskQueue,
                                                                                         TWfArg workflowArg,
                                                                                         string signalName,
                                                                                         TSigArg signalArg,
                                                                                         StartWorkflowConfiguration workflowConfig = null,
                                                                                         CancellationToken cancelToken = default)
        {
            IWorkflowHandle workflow = CreateWorkflowHandle(workflowId);
            await workflow.SignalWithStartAsync<TWfArg, TSigArg>(workflowTypeName, taskQueue,
                                                                 workflowArg,
                                                                 signalName,
                                                                 signalArg,
                                                                 workflowConfig,
                                                                 cancelToken);
            return workflow;
        }
        #endregion SignalWorkflowWithStartAsync(..)


        #region CreateWorkflowHandle(..)        

        /// <summary>
        /// Create an unbound workflow chain handle.
        /// The handle will be bound to the most recent chain with the specified <c>workflowId</c> once the user invokes an API
        /// the end up interacting with some workflow run on the server.
        /// </summary>
        public IWorkflowHandle CreateWorkflowHandle(string workflowId)
        {
            return WorkflowHandle.CreateUnbound(this, workflowId);
        }

        /// <summary>
        /// Create an workflow chain handle that represents a workflow chain with the specified <c>workflowId</c> and <c>workflowChainId</c>.
        /// </summary>
        /// <param name="workflowId">The workflow-id of the workflow chain that will be represented by the newly created handle.</param>
        /// <param name="workflowChainId">The workflow-run-id of the <em>first workflow run</em> of the <em>workflow chain</em> represented
        /// by the newly created handle.</param>
        public IWorkflowHandle CreateWorkflowHandle(string workflowId,
                                                    string workflowChainId)
        {
            return WorkflowHandle.CreateBound(this, workflowId, workflowChainId);
        }
        #endregion CreateWorkflowHandle(..)


        #region CreateWorkflowRunHandle(..)

        /// <summary>
        /// Create an workflow run handle that represents a workflow run with the specified <c>workflowId</c> and <c>workflowRunId</c>.
        /// </summary>
        public IWorkflowRunHandle CreateWorkflowRunHandle(string workflowId,
                                                          string workflowRunId)
        {
            return new WorkflowRunHandle(this, workflowId, workflowRunId);
        }
        #endregion CreateWorkflowRunHandle(..)

        #endregion -- Workflow access and control APIs --


        #region -- Connection management --

        public bool IsConnected { get; private set; }

        /// <summary>
        /// <para>Ensure that the connection to the server is initialized and valid.</para>
        /// <para>The default implementation of this iface (<see cref="TemporalClient" />) has a factory method that created a client
        /// instace with a readily initialized connection (<see cref="TemporalClient.ConnectAsync" />). However, implementations
        /// of this iface may choose not to provide such a factory method. Users of such implementations can use this API to
        /// pro-actively initialize the server connection.<br/>
        /// This method must be a no-op, if the connection is already initialized.</para>
        /// <para>Implementations that use the Temporal server need to initialize the underlying connection by executing
        /// GetSystemInfo(..) to check the server health and get server capabilities. This API will explicitly perform that.
        /// If this API is not explicitly invoked by the user, implementations must ensure it is automaticlly invoked before placing
        /// any other calls.</para>
        /// </summary>
        /// <remarks>The default implementation of an <c>ITemporalClient</c> is <see cref="TemporalClient" />.
        /// It is recommended to use the factory method <see cref="TemporalClient.ConnectAsync" /> to create instanced of
        /// <c>TemporalClient</c>, and in such cases it is not required to explicitly call <c>EnsureConnectedAsync</c> on
        /// a new client instace (calling it will be a no-op).
        /// However, in some specific cases the user may NOT want to initialize the connection at client creation.
        /// For example, some clients require CancellationToken support. In some other scenarios, where the client is initialized by a
        /// dependency injection container, the user may not want to interact with the network until the dependency resolution is complete.
        /// In such cases, it is possible to create an instance of <c>TemporalClient</c> using the constructor. In such cases the client
        /// will automatically initialize its connection before it is used for the first time. However, in such scenarios, applications must
        /// be aware of the additional latency and possible errors which may occur during connection initialization.
        /// Invoking <c>EnsureConnectedAsync</c> will initialize the connection as a controled point in time where the user can
        /// control any such side-effects.
        /// </remarks>
        public Task EnsureConnectedAsync(CancellationToken cancelToken = default)
        {
            if (IsConnected)
            {
                return Task.CompletedTask;
            }

            return ConnectAndValidateAsync(cancelToken);
        }

        private Task ConnectAndValidateAsync(CancellationToken cancelToken)
        {
            // @ToDo: Call server to get capabilities
            cancelToken.ThrowIfCancellationRequested();
            IsConnected = true;
            return Task.CompletedTask;
        }

        #endregion -- Connection management --


        #region -- Service invocation pipeline management --

        /// <summary>
        /// <c>WorkflowHandle</c> and <c>WorkflowRunHandle</c> instances call this to create the invocation pipelie for themselves.        
        /// </summary>
        internal ITemporalClientInterceptor GetOrCreateServiceInvocationPipeline(object initialPipelineInvoker,
                                                                                 ref ITemporalClientInterceptor pipelineStorageRef,
                                                                                 object pipelineCreationLock,
                                                                                 IWorkflowOperationArguments initialOperationArguments)
        {

            ITemporalClientInterceptor pipeline = pipelineStorageRef;

            if (pipeline == null)
            {
                lock (pipelineCreationLock)
                {
                    pipeline = pipelineStorageRef;

                    if (pipeline == null)
                    {
                        pipeline = CreateServiceInvocationPipeline(initialPipelineInvoker, initialOperationArguments);
                        pipelineStorageRef = pipeline;
                    }
                }
            }

            return pipeline;
        }

        private ITemporalClientInterceptor CreateServiceInvocationPipeline(object initialPipelineInvoker,
                                                                           IWorkflowOperationArguments initialOperationArguments)
        {
            // Create default interceptor pipelie for all workflows (tracing etc..):

            List<ITemporalClientInterceptor> pipeline = CreateDefaultServiceInvocationPipeline();

            // Construct the property bag used for passing information to pipeline item factories:

            ServiceInvocationPipelineItemFactoryArguments pipelineItemFactoryArguments = new(this,
                                                                                             initialPipelineInvoker,
                                                                                             initialOperationArguments);

            // Apply custom interceptor factory:

            Action<ServiceInvocationPipelineItemFactoryArguments, IList<ITemporalClientInterceptor>> customInterceptorFactory = Configuration.ClientInterceptorFactory;
            if (customInterceptorFactory != null)
            {
                customInterceptorFactory(pipelineItemFactoryArguments, pipeline);
            }

            // Now we need to add the final interceptor, aka the "sink".

            // Create the payload converter for the sink:

            IPayloadConverter payloadConverter = null;
            Func<ServiceInvocationPipelineItemFactoryArguments, IPayloadConverter> customPayloadConverterFactory = Configuration.PayloadConverterFactory;
            if (customPayloadConverterFactory != null)
            {
                payloadConverter = customPayloadConverterFactory(pipelineItemFactoryArguments);
            }

            if (payloadConverter == null)
            {
                payloadConverter = new CompositePayloadConverter();
            }

            // Create the payload codec for the sink:

            IPayloadCodec payloadCodec = null;
            Func<ServiceInvocationPipelineItemFactoryArguments, IPayloadCodec> customPayloadCodecFactory = Configuration.PayloadCodecFactory;
            if (customPayloadCodecFactory != null)
            {
                payloadCodec = customPayloadCodecFactory(pipelineItemFactoryArguments);
            }

            // Create the sink:

            WorkflowServiceClientEnvelope grpcClientEnvelope = GetOrCreateGrpcClientEnvelope();

            ILogger<TemporalServiceInvoker> logger = (Configuration.LoggerFactory ?? Configuration.LoggerFactory).CreateLogger<TemporalServiceInvoker>();
            ITemporalClientInterceptor downstream = new TemporalServiceInvoker(grpcClientEnvelope,
                                                                               _identityMarker,
                                                                               payloadConverter,
                                                                               payloadCodec,
                                                                               logger);

            // Build the pipeline by creating the chain of interceptors ending with the sink:

            downstream.Init(nextInterceptor: null);

            for (int i = pipeline.Count - 1; i >= 0; i--)
            {
                ITemporalClientInterceptor current = pipeline[i];
                if (current != null)
                {
                    current.Init(downstream);
                    downstream = current;
                }
            }

            return downstream;
        }

        private List<ITemporalClientInterceptor> CreateDefaultServiceInvocationPipeline()
        {
            List<ITemporalClientInterceptor> pipeline = new();
            return pipeline;
        }

        private WorkflowServiceClientEnvelope GetOrCreateGrpcClientEnvelope()
        {
            WorkflowServiceClientEnvelope grpcClientEnvelope = _grpcServiceClientEnvelope;
            while (grpcClientEnvelope == null)
            {
                WorkflowServiceClientEnvelope newEnvelope = (Configuration.CustomGrpcWorkflowServiceClient != null)
                                        ? new WorkflowServiceClientEnvelope(Configuration.CustomGrpcWorkflowServiceClient)
                                        : WorkflowServiceClientFactory.SingletonInstance.GetOrCreateClient(Configuration.ServiceConnection);

                if (newEnvelope.TryAddRef())
                {
                    grpcClientEnvelope = Concurrent.TrySetOrGetValue(ref _grpcServiceClientEnvelope, newEnvelope, out bool stored);
                    if (!stored)
                    {
                        newEnvelope.Release();
                    }
                }
            }

            return grpcClientEnvelope;
        }

        #endregion -- Service invocation pipeline management --

        #region -- Dispose --

        private void Dispose(bool disposing)
        {
            try
            {
                WorkflowServiceClientEnvelope grpcClientEnvelope = Interlocked.Exchange(ref _grpcServiceClientEnvelope, null);
                if (grpcClientEnvelope != null)
                {
                    grpcClientEnvelope.Release();
                }
            }
            catch (Exception ex)
            {
                if (disposing)
                {
                    // Only rethrow if not on finalizer thread.
                    // @ToDo: once we have logging, log if on finalizer thread.
                    ExceptionDispatchInfo.Capture(ex).Throw();
                }
            }
        }

        ~TemporalClient()
        {
            // Do not change this code. Put cleanup code in `Dispose(bool disposing)` method.
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in `Dispose(bool disposing)` method.
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion -- Dispose --
    }
}
