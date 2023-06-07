namespace Temporalio.Tests.Worker;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Temporalio.Api.Common.V1;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Worker.Interceptors;
using Temporalio.Workflows;

public class HeaderCallbackInterceptor : IClientInterceptor, IWorkerInterceptor
{
    public Func<string, IDictionary<string, Payload>?>? OnOutbound { get; set; }

    public Action<string, IReadOnlyDictionary<string, Payload>?>? OnInbound { get; set; }

    public ClientOutboundInterceptor InterceptClient(
        ClientOutboundInterceptor nextInterceptor) =>
        new ClientOutbound(this, nextInterceptor);

    public WorkflowInboundInterceptor InterceptWorkflow(
        WorkflowInboundInterceptor nextInterceptor) =>
        new WorkflowInbound(this, nextInterceptor);

    public ActivityInboundInterceptor InterceptActivity(
        ActivityInboundInterceptor nextInterceptor) =>
        new ActivityInbound(this, nextInterceptor);

    private sealed class ClientOutbound : ClientOutboundInterceptor
    {
        private readonly HeaderCallbackInterceptor root;

        internal ClientOutbound(HeaderCallbackInterceptor root, ClientOutboundInterceptor next)
            : base(next) => this.root = root;

        public override Task<WorkflowHandle<TWorkflow, TResult>> StartWorkflowAsync<TWorkflow, TResult>(
            StartWorkflowInput input)
        {
            input = input with { Headers = root.OnOutbound?.Invoke("Client:StartWorkflow") };
            return base.StartWorkflowAsync<TWorkflow, TResult>(input);
        }

        public override Task SignalWorkflowAsync(SignalWorkflowInput input)
        {
            input = input with { Headers = root.OnOutbound?.Invoke("Client:SignalWorkflow") };
            return base.SignalWorkflowAsync(input);
        }

        public override Task<TResult> QueryWorkflowAsync<TResult>(QueryWorkflowInput input)
        {
            input = input with { Headers = root.OnOutbound?.Invoke("Client:QueryWorkflow") };
            return base.QueryWorkflowAsync<TResult>(input);
        }
    }

    private sealed class WorkflowInbound : WorkflowInboundInterceptor
    {
        private readonly HeaderCallbackInterceptor root;

        internal WorkflowInbound(HeaderCallbackInterceptor root, WorkflowInboundInterceptor next)
            : base(next) => this.root = root;

        public override void Init(WorkflowOutboundInterceptor outbound) =>
            base.Init(new WorkflowOutbound(root, outbound));

        public override Task<object?> ExecuteWorkflowAsync(ExecuteWorkflowInput input)
        {
            root.OnInbound?.Invoke("Workflow:ExecuteWorkflow", Workflow.Info.Headers);
            return base.ExecuteWorkflowAsync(input);
        }

        public override Task HandleSignalAsync(HandleSignalInput input)
        {
            root.OnInbound?.Invoke("Workflow:HandleSignal", input.Headers);
            return base.HandleSignalAsync(input);
        }

        public override object? HandleQuery(HandleQueryInput input)
        {
            root.OnInbound?.Invoke("Workflow:HandleQuery", input.Headers);
            return base.HandleQuery(input);
        }
    }

    private sealed class WorkflowOutbound : WorkflowOutboundInterceptor
    {
        private readonly HeaderCallbackInterceptor root;

        internal WorkflowOutbound(HeaderCallbackInterceptor root, WorkflowOutboundInterceptor next)
            : base(next) => this.root = root;

        public override ContinueAsNewException CreateContinueAsNewException(CreateContinueAsNewExceptionInput input)
        {
            input = input with { Headers = root.OnOutbound?.Invoke("Workflow:ContinueAsNew") };
            return base.CreateContinueAsNewException(input);
        }

        public override Task<TResult> ScheduleActivityAsync<TResult>(
            ScheduleActivityInput input)
        {
            input = input with { Headers = root.OnOutbound?.Invoke("Workflow:ScheduleActivity") };
            return base.ScheduleActivityAsync<TResult>(input);
        }

        public override Task<TResult> ScheduleLocalActivityAsync<TResult>(
            ScheduleLocalActivityInput input)
        {
            input = input with { Headers = root.OnOutbound?.Invoke("Workflow:ScheduleLocalActivity") };
            return base.ScheduleLocalActivityAsync<TResult>(input);
        }

        public override Task SignalChildWorkflowAsync(SignalChildWorkflowInput input)
        {
            input = input with { Headers = root.OnOutbound?.Invoke("Workflow:SignalChildWorkflow") };
            return base.SignalChildWorkflowAsync(input);
        }

        public override Task SignalExternalWorkflowAsync(SignalExternalWorkflowInput input)
        {
            input = input with { Headers = root.OnOutbound?.Invoke("Workflow:SignalExternalWorkflow") };
            return base.SignalExternalWorkflowAsync(input);
        }

        public override Task<ChildWorkflowHandle<TWorkflow, TResult>> StartChildWorkflowAsync<TWorkflow, TResult>(
            StartChildWorkflowInput input)
        {
            input = input with { Headers = root.OnOutbound?.Invoke("Workflow:StartChildWorkflow") };
            return base.StartChildWorkflowAsync<TWorkflow, TResult>(input);
        }
    }

    private sealed class ActivityInbound : ActivityInboundInterceptor
    {
        private readonly HeaderCallbackInterceptor root;

        internal ActivityInbound(HeaderCallbackInterceptor root, ActivityInboundInterceptor next)
            : base(next) => this.root = root;

        public override Task<object?> ExecuteActivityAsync(ExecuteActivityInput input)
        {
            root.OnInbound?.Invoke("Activity:ExecuteActivity", input.Headers);
            return base.ExecuteActivityAsync(input);
        }
    }
}
