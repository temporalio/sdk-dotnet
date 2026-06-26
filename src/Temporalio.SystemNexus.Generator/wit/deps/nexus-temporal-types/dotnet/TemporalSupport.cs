using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Google.Protobuf.WellKnownTypes;
using Temporalio.Common;
using Temporalio.Converters;
using Temporalio.Workflows;
using ApiCommon = Temporalio.Api.Common.V1;
using ApiDeployment = Temporalio.Api.Deployment.V1;
using ApiTaskQueue = Temporalio.Api.TaskQueue.V1;
using ApiWorkflow = Temporalio.Api.Workflow.V1;

namespace NexGen.Support
{
    internal static class TemporalWorkflowContext
    {
        internal static string WorkflowNamespace() => Workflow.Info.Namespace;
    }

    internal static class TemporalFunctionNames
    {
        internal static (MethodInfo Method, IReadOnlyCollection<object?> Args) ExtractCall<TInstance, TResult>(
            Expression<Func<TInstance, TResult>> expression) => ExpressionUtil.ExtractCall(expression);

        internal static string WorkflowName(MethodInfo method)
        {
            if (method.GetCustomAttribute<WorkflowRunAttribute>() == null)
            {
                throw new ArgumentException($"{method} missing WorkflowRun attribute");
            }
            var definition = WorkflowDefinition.Create(method.ReflectedType ??
                throw new ArgumentException($"{method} has no reflected type"));
            return definition.Name ??
                throw new ArgumentException(
                    $"{method} cannot be used directly since it is a dynamic workflow");
        }

        internal static string SignalName(MethodInfo method)
        {
            var definition = WorkflowSignalDefinition.FromMethod(method);
            return definition.Name ??
                throw new ArgumentException(
                    $"{method} cannot be used directly since it is a dynamic signal");
        }
    }

    internal static class ProtoExtensions
    {
        internal static ApiCommon.WorkflowType ToWorkflowTypeProto(this string value) =>
            new() { Name = value };

        internal static ApiTaskQueue.TaskQueue ToTaskQueueProto(this string value) =>
            new() { Name = value };

        internal static ApiCommon.Payload ToPayload(object? value) =>
            Workflow.PayloadConverter.ToPayload(value);

        internal static ApiCommon.Payloads ToPayloads(IEnumerable<object?> values)
        {
            var payloads = new ApiCommon.Payloads();
            payloads.Payloads_.AddRange(Workflow.PayloadConverter.ToPayloads(values as IReadOnlyCollection<object?> ?? new List<object?>(values)));
            return payloads;
        }

        internal static Duration ToProto(this TimeSpan value) =>
            Duration.FromTimeSpan(value);

        internal static ApiCommon.RetryPolicy ToProto(this Temporalio.Common.RetryPolicy value) =>
            ToRetryPolicy(value);

        internal static ApiCommon.Memo ToProto(this IReadOnlyDictionary<string, object?> value) =>
            ToMemo(value);

        internal static ApiCommon.Priority ToProto(this Temporalio.Common.Priority value) =>
            ToPriority(value);

        internal static ApiWorkflow.VersioningOverride ToProto(this Temporalio.Common.VersioningOverride value) =>
            ToVersioningOverride(value);

        private static ApiCommon.RetryPolicy ToRetryPolicy(Temporalio.Common.RetryPolicy policy)
        {
            var proto = new ApiCommon.RetryPolicy
            {
                InitialInterval = Duration.FromTimeSpan(policy.InitialInterval),
                BackoffCoefficient = policy.BackoffCoefficient,
                MaximumAttempts = policy.MaximumAttempts,
            };
            if (policy.MaximumInterval is { } maximumInterval)
            {
                proto.MaximumInterval = Duration.FromTimeSpan(maximumInterval);
            }
            if (policy.NonRetryableErrorTypes is { Count: > 0 } nonRetryableErrorTypes)
            {
                proto.NonRetryableErrorTypes.AddRange(nonRetryableErrorTypes);
            }
            return proto;
        }

        private static ApiCommon.Memo ToMemo(IReadOnlyDictionary<string, object?> memo)
        {
            var proto = new ApiCommon.Memo();
            foreach (var item in memo)
            {
                if (item.Value == null)
                {
                    throw new ArgumentException($"Memo value for {item.Key} is null", nameof(memo));
                }
                proto.Fields.Add(item.Key, Workflow.PayloadConverter.ToPayload(item.Value));
            }
            return proto;
        }

        private static ApiCommon.Priority ToPriority(Temporalio.Common.Priority priority) => new()
        {
            PriorityKey = priority.PriorityKey ?? 0,
            FairnessKey = priority.FairnessKey ?? string.Empty,
            FairnessWeight = priority.FairnessWeight ?? 0f,
        };

        private static ApiWorkflow.VersioningOverride ToVersioningOverride(Temporalio.Common.VersioningOverride versioningOverride) =>
            versioningOverride switch
            {
                Temporalio.Common.VersioningOverride.Pinned pinned => new ApiWorkflow.VersioningOverride
                {
#pragma warning disable CS0612
                    Behavior = Temporalio.Api.Enums.V1.VersioningBehavior.Pinned,
                    PinnedVersion = pinned.Version.ToCanonicalString(),
#pragma warning restore CS0612
                    Pinned = new ApiWorkflow.VersioningOverride.Types.PinnedOverride
                    {
                        Version = new ApiDeployment.WorkerDeploymentVersion
                        {
                            DeploymentName = pinned.Version.DeploymentName,
                            BuildId = pinned.Version.BuildId,
                        },
                        Behavior = (ApiWorkflow.VersioningOverride.Types.PinnedOverrideBehavior)pinned.Behavior,
                    },
                },
                Temporalio.Common.VersioningOverride.AutoUpgrade _ => new ApiWorkflow.VersioningOverride
                {
#pragma warning disable CS0612
                    Behavior = Temporalio.Api.Enums.V1.VersioningBehavior.AutoUpgrade,
#pragma warning restore CS0612
                    AutoUpgrade = true,
                },
                _ => throw new ArgumentException("Unknown versioning override type", nameof(versioningOverride)),
            };
    }
}
