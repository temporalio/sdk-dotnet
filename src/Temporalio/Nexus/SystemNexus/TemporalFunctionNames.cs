#pragma warning disable SA1600 // Internal implementation plumbing.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Temporalio.Workflows;

namespace Temporalio.Nexus.SystemNexus
{
    internal static class TemporalFunctionNames
    {
        internal static (MethodInfo Method, IReadOnlyCollection<object?> Args) ExtractCall<TDelegate>(
            Expression<TDelegate> expression)
        {
            if (expression.Body is not MethodCallExpression call)
            {
                throw new ArgumentException("Expression must be a single method call", nameof(expression));
            }

            var method = call.Method;
            var args = call.Arguments.Select(arg => Expression.Lambda<Func<object?>>(
                Expression.Convert(arg, typeof(object))).Compile()()).ToArray();
            return (method, args);
        }

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
}
