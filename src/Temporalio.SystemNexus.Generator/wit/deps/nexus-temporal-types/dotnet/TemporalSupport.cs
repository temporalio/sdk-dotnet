using System;
using System.Collections.Generic;
using System.Linq;
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
        internal static ApiCommon.WorkflowType ToProto(this string value, ApiCommon.WorkflowType _) =>
            new() { Name = value };

        internal static ApiTaskQueue.TaskQueue ToProto(this string value, ApiTaskQueue.TaskQueue _) =>
            new() { Name = value };

        internal static ApiCommon.Payload ToProto(this object? value) =>
            Workflow.PayloadConverter.ToPayload(value);

        internal static ApiCommon.Payloads ToProto(this IEnumerable<object?> value) =>
            ToPayloads(value);

        internal static Duration ToProto(this TimeSpan value) =>
            Duration.FromTimeSpan(value);

        internal static ApiCommon.RetryPolicy ToProto(this Temporalio.Common.RetryPolicy value) =>
            ToRetryPolicy(value);

        internal static ApiCommon.Memo ToProto(this IReadOnlyDictionary<string, object?> value) =>
            ToMemo(value);

        internal static ApiCommon.SearchAttributes ToProto(this SearchAttributeCollection value) =>
            value.ToProto();

        internal static ApiCommon.Priority ToProto(this Temporalio.Common.Priority value) =>
            ToPriority(value);

        internal static ApiWorkflow.VersioningOverride ToProto(this Temporalio.Common.VersioningOverride value) =>
            ToVersioningOverride(value);

        internal static string FromProto(this ApiCommon.WorkflowType proto)
        {
            if (proto is null)
            {
                throw new ArgumentNullException(nameof(proto));
            }
            return proto.Name;
        }

        internal static string FromProto(this ApiTaskQueue.TaskQueue proto)
        {
            if (proto is null)
            {
                throw new ArgumentNullException(nameof(proto));
            }
            return proto.Name;
        }

        internal static TimeSpan FromProto(this Duration proto)
        {
            if (proto is null)
            {
                throw new ArgumentNullException(nameof(proto));
            }
            return proto.ToTimeSpan();
        }

        internal static object? FromProto(this ApiCommon.Payload proto)
        {
            if (proto is null)
            {
                throw new ArgumentNullException(nameof(proto));
            }
            return Workflow.PayloadConverter.ToValue<object?>(proto);
        }

        internal static IReadOnlyCollection<object?> FromProto(this ApiCommon.Payloads proto)
        {
            if (proto is null)
            {
                throw new ArgumentNullException(nameof(proto));
            }
            return PayloadsToValues(proto);
        }

        internal static Temporalio.Common.RetryPolicy FromProto(this ApiCommon.RetryPolicy proto)
        {
            if (proto is null)
            {
                throw new ArgumentNullException(nameof(proto));
            }
            return FromRetryPolicy(proto);
        }

        internal static IReadOnlyDictionary<string, object?> FromProto(this ApiCommon.Memo proto)
        {
            if (proto is null)
            {
                throw new ArgumentNullException(nameof(proto));
            }
            return proto.Fields.ToDictionary(item => item.Key, item => Workflow.PayloadConverter.ToValue<object?>(item.Value));
        }

        internal static SearchAttributeCollection FromProto(this ApiCommon.SearchAttributes proto)
        {
            if (proto is null)
            {
                throw new ArgumentNullException(nameof(proto));
            }
            return SearchAttributeCollection.FromProto(proto);
        }

        internal static Temporalio.Common.Priority FromProto(this ApiCommon.Priority proto)
        {
            if (proto is null)
            {
                throw new ArgumentNullException(nameof(proto));
            }
            return new Temporalio.Common.Priority(
                proto.PriorityKey == 0 ? null : proto.PriorityKey,
                string.IsNullOrEmpty(proto.FairnessKey) ? null : proto.FairnessKey,
                proto.FairnessWeight == 0f ? null : proto.FairnessWeight);
        }

        internal static Temporalio.Common.VersioningOverride FromProto(this ApiWorkflow.VersioningOverride proto)
        {
            if (proto is null)
            {
                throw new ArgumentNullException(nameof(proto));
            }
            if (proto.Pinned is { } pinned)
            {
                return new Temporalio.Common.VersioningOverride.Pinned(
                    new WorkerDeploymentVersion(pinned.Version.DeploymentName, pinned.Version.BuildId),
                    (Temporalio.Common.VersioningOverride.PinnedOverrideBehavior)pinned.Behavior);
            }
            if (proto.AutoUpgrade)
            {
                return new Temporalio.Common.VersioningOverride.AutoUpgrade();
            }

            throw new NotSupportedException("Unsupported versioning override proto");
        }

        private static ApiCommon.Payloads ToPayloads(IEnumerable<object?> values)
        {
            var payloads = new ApiCommon.Payloads();
            payloads.Payloads_.AddRange(Workflow.PayloadConverter.ToPayloads(values as IReadOnlyCollection<object?> ?? new List<object?>(values)));
            return payloads;
        }

        private static IReadOnlyCollection<object?> PayloadsToValues(ApiCommon.Payloads payloads) =>
            payloads.Payloads_.Select(payload => Workflow.PayloadConverter.ToValue<object?>(payload)).ToArray();

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

        private static Temporalio.Common.RetryPolicy FromRetryPolicy(ApiCommon.RetryPolicy proto)
        {
            var retryPolicy = new Temporalio.Common.RetryPolicy
            {
                BackoffCoefficient = (float)proto.BackoffCoefficient,
                MaximumAttempts = proto.MaximumAttempts,
                NonRetryableErrorTypes = proto.NonRetryableErrorTypes.ToArray(),
            };
            if (proto.InitialInterval is { } initialInterval)
            {
                retryPolicy.InitialInterval = initialInterval.ToTimeSpan();
            }
            if (proto.MaximumInterval is { } maximumInterval)
            {
                retryPolicy.MaximumInterval = maximumInterval.ToTimeSpan();
            }
            return retryPolicy;
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
