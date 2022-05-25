using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;
using Google.Protobuf.WellKnownTypes;
using Xunit;
using Xunit.Abstractions;

using Temporal.Api.Common.V1;
using Temporal.Api.Enums.V1;
using Temporal.Api.History.V1;
using Temporal.Serialization;
using Temporal.TestUtil;
using Temporal.WorkflowClient;
using Temporal.WorkflowClient.OperationConfigurations;

namespace Temporal.Sdk.WorkflowClient.Test.Int
{
    [Collection("SequentialTestExecution")]
    public abstract class TemporalClientTestBase : IntegrationTestBase
    {
        private ITemporalClient _client = null;
        private ExtendedWorkflowServiceClient _wfServiceClient = null;

        public TemporalClientTestBase(ITestOutputHelper cout, TestTlsOptions tlsOptions, int temporalServicePort = 7233)
            : base(cout, temporalServicePort, tlsOptions)
        {
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            TemporalClient client = CreateTemporalClient();
            _client = client;
            _wfServiceClient = new ExtendedWorkflowServiceClient(client.Configuration);
        }

        public override async Task DisposeAsync()
        {
            ExtendedWorkflowServiceClient wfServiceClient = Interlocked.Exchange(ref _wfServiceClient, null);
            if (wfServiceClient != null)
            {
                wfServiceClient.Dispose();
            }

            ITemporalClient client = Interlocked.Exchange(ref _client, null);
            if (client != null)
            {
                client.Dispose();
            }

            await base.DisposeAsync();
        }

        [Fact]
        public async Task ConnectAsync()
        {
            await Task.Delay(1);
        }

        [Fact]
        public void Ctor_Plain()
        {
        }

        [Fact]
        public void Ctor_WithClientConfiguration()
        {

        }

        [Fact]
        public async Task StartWorkflowAsync_NoWfArgs()
        {
            const string WfTypeName = "TestWorkflowTypeName";
            string wfId = TestCaseWorkflowId();
            string taskQueue = TestCaseTaskQueue();

            await _client.StartWorkflowAsync(wfId, WfTypeName, taskQueue);

            List<HistoryEvent> history = await _wfServiceClient.GetHistoryAsync(wfId, 2);

            history.Should().NotBeNull();
            history.Should().HaveCount(2);
            {
                history[0].Should().NotBeNull();
                history[0].EventType.Should().Be(EventType.WorkflowExecutionStarted);
                WorkflowExecutionStartedEventAttributes eventAttrs = history[0].WorkflowExecutionStartedEventAttributes;

                eventAttrs.Should().NotBeNull();
                eventAttrs.WorkflowType?.Name.Should().Be(WfTypeName);
                eventAttrs.TaskQueue?.Name.Should().Be(taskQueue);
                eventAttrs.Input.Payloads_.Should().BeEmpty();
                eventAttrs.FirstExecutionRunId.Should().NotBeNullOrWhiteSpace();
            }
            {
                history[1].Should().NotBeNull();
                history[1].EventType.Should().Be(EventType.WorkflowTaskScheduled);
                WorkflowTaskScheduledEventAttributes eventAttrs = history[1].WorkflowTaskScheduledEventAttributes;

                eventAttrs.Should().NotBeNull();
                eventAttrs.TaskQueue?.Name.Should().Be(taskQueue);
            }
        }

        [Fact]
        public async Task StartWorkflowAsync_WithWfArgs()
        {
            const string WfTypeName = "TestWorkflowTypeName";
            string wfId = TestCaseWorkflowId();
            string taskQueue = TestCaseTaskQueue();

            WorkflowInputPayload wfInput = new();
            await _client.StartWorkflowAsync(wfId, WfTypeName, taskQueue, wfInput);

            List<HistoryEvent> history = await _wfServiceClient.GetHistoryAsync(wfId, 2);

            history.Should().NotBeNull();
            history.Should().HaveCount(2);

            {
                history[0].Should().NotBeNull();
                history[0].EventType.Should().Be(EventType.WorkflowExecutionStarted);
                WorkflowExecutionStartedEventAttributes eventAttrs = history[0].WorkflowExecutionStartedEventAttributes;

                eventAttrs.Should().NotBeNull();
                eventAttrs.WorkflowType?.Name.Should().Be(WfTypeName);
                eventAttrs.TaskQueue?.Name.Should().Be(taskQueue);
                eventAttrs.Input.Payloads_.Should().HaveCount(1);

                eventAttrs.FirstExecutionRunId.Should().NotBeNullOrWhiteSpace();

                WorkflowInputPayload inputInHistory = await PayloadConverter.DeserializeAsync<WorkflowInputPayload>(
                                                                    converter: new CompositePayloadConverter(),
                                                                    codec: null,
                                                                    serializedData: eventAttrs.Input,
                                                                    cancelToken: CancellationToken.None);

                inputInHistory.Should().Be(wfInput);
            }
            {
                history[1].Should().NotBeNull();
                history[1].EventType.Should().Be(EventType.WorkflowTaskScheduled);
                WorkflowTaskScheduledEventAttributes eventAttrs = history[1].WorkflowTaskScheduledEventAttributes;

                eventAttrs.Should().NotBeNull();
                eventAttrs.TaskQueue?.Name.Should().Be(taskQueue);
            }
        }

        [Fact]
        public async Task StartWorkflowAsync_Cancel()
        {
            const string WfTypeName = "TestWorkflowTypeName";
            string wfId = TestCaseWorkflowId();
            string taskQueue = TestCaseTaskQueue();

            using CancellationTokenSource cancelControl = new();
            cancelControl.Cancel();

            Func<Task> startWfFunc = async () => await _client.StartWorkflowAsync(wfId, WfTypeName, taskQueue, cancelToken: cancelControl.Token);

            await startWfFunc.Should().ThrowAsync<OperationCanceledException>()
                                      .Where((ocEx) => ocEx.CancellationToken == cancelControl.Token);
        }

        [Fact]
        public async Task StartWorkflowAsync_WithStartWfConfig()
        {
            const string WfTypeName = "TestWorkflowTypeName";
            string wfId = TestCaseWorkflowId();
            string taskQueue = TestCaseTaskQueue();

            StartWorkflowConfiguration startWfConfig = new(WorkflowExecutionTimeout: TimeSpan.FromMinutes(30),
                                                           WorkflowRunTimeout: TimeSpan.FromMinutes(20),
                                                           WorkflowTaskTimeout: TimeSpan.FromMilliseconds(2500),
                                                           WorkflowIdReusePolicy: WorkflowIdReusePolicy.AllowDuplicateFailedOnly,
                                                           RetryPolicy: new RetryPolicy()
                                                           {
                                                               InitialInterval = Duration.FromTimeSpan(TimeSpan.FromMilliseconds(500)),
                                                               BackoffCoefficient = 1.5,
                                                               MaximumInterval = Duration.FromTimeSpan(TimeSpan.FromSeconds(15)),
                                                               MaximumAttempts = 11
                                                           },
                                                           CronSchedule: String.Empty,
                                                           Memo: null,
                                                           SearchAttributes: null,
                                                           Header: null);

            await _client.StartWorkflowAsync(wfId, WfTypeName, taskQueue, workflowConfig: startWfConfig);

            List<HistoryEvent> history = await _wfServiceClient.GetHistoryAsync(wfId, 2);

            history.Should().NotBeNull();
            history.Should().HaveCount(2);

            {
                history[0].Should().NotBeNull();
                history[0].EventType.Should().Be(EventType.WorkflowExecutionStarted);
                WorkflowExecutionStartedEventAttributes eventAttrs = history[0].WorkflowExecutionStartedEventAttributes;

                eventAttrs.Should().NotBeNull();
                eventAttrs.WorkflowType?.Name.Should().Be(WfTypeName);
                eventAttrs.TaskQueue?.Name.Should().Be(taskQueue);
                eventAttrs.Input.Payloads_.Should().BeEmpty();
                eventAttrs.FirstExecutionRunId.Should().NotBeNullOrWhiteSpace();

                eventAttrs.WorkflowExecutionTimeout.Should().Be(Duration.FromTimeSpan(TimeSpan.FromMinutes(30)));
                eventAttrs.WorkflowRunTimeout.Should().Be(Duration.FromTimeSpan(TimeSpan.FromMinutes(20)));
                eventAttrs.WorkflowTaskTimeout.Should().Be(Duration.FromTimeSpan(TimeSpan.FromSeconds(2.5)));
                startWfConfig.RetryPolicy.Should().Be(eventAttrs.RetryPolicy);
            }
            {
                history[1].Should().NotBeNull();
                history[1].EventType.Should().Be(EventType.WorkflowTaskScheduled);
                WorkflowTaskScheduledEventAttributes eventAttrs = history[1].WorkflowTaskScheduledEventAttributes;

                eventAttrs.Should().NotBeNull();
                eventAttrs.TaskQueue?.Name.Should().Be(taskQueue);
            }
        }

        #region record WorkflowInputPayload
        private record WorkflowInputPayload(string StrData, int Int32Data, IList<double> ListOfDoubles, WorkflowInputPayload SubPayload)
        {
            public WorkflowInputPayload()
                : this("Test-String-Data",
                       42,
                       new[] { 100.0, 200.1, 300.2 },
                       new WorkflowInputPayload("Test-String-SubData",
                                                1042,
                                                new[] { 0.1, 0.2001, 0.3002 },
                                                null))
            {
            }

            public virtual bool Equals(WorkflowInputPayload other)
            {
                if (other == null)
                {
                    return false;
                }

                return String.Equals(StrData, other.StrData, StringComparison.Ordinal)
                            && Int32Data == other.Int32Data
                            && Enumerable.SequenceEqual(ListOfDoubles, other.ListOfDoubles)
                            && (SubPayload == null) ? (other.SubPayload == null) : SubPayload.Equals(other.SubPayload);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }
        #endregion record WorkflowInputPayload
    }
}

