using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Temporalio.Workflow
{
    /// <summary>
    /// Internal representation of a workflow definition.
    /// </summary>
    /// <param name="Name">Workflow type name.</param>
    /// <param name="Type">Type of the workflow.</param>
    /// <param name="RunMethod">Workflow entry point.</param>
    internal record WorkflowDefinition(
        string Name,
        Type Type,
        MethodInfo RunMethod) // TODO(cretz): Include signals and queries and such
    {
        private static readonly ConcurrentDictionary<Type, WorkflowDefinition> DefinitionsByType = new();

        /// <summary>
        /// Get a workflow definition for the given type or fail. The result is cached.
        /// </summary>
        /// <param name="type">Type to get definition for.</param>
        /// <returns>Definition for the type.</returns>
        public static WorkflowDefinition FromType(Type type)
        {
            return DefinitionsByType.GetOrAdd(type, CreateFromType);
        }

        /// <summary>
        /// Get a workflow definition for the given workflow run method or fail. The result is
        /// cached.
        /// </summary>
        /// <param name="runMethod">Method with a <see cref="WorkflowRunAttribute" />.</param>
        /// <returns>Definition for the type.</returns>
        public static WorkflowDefinition FromRunMethod(MethodInfo runMethod)
        {
            if (runMethod.GetCustomAttribute<WorkflowRunAttribute>() == null)
            {
                throw new ArgumentException($"{runMethod} missing WorkflowRun attribute");
            }
            // We intentionally use reflected type because we don't allow inheritance of run methods
            // in any way, they must be explicitly defined on the type
            return FromType(runMethod.ReflectedType ??
                throw new ArgumentException($"{runMethod} has no reflected type"));
        }

        private static WorkflowDefinition CreateFromType(Type type)
        {
            // Get the main attribute
            var attr = type.GetCustomAttribute<WorkflowAttribute>(false) ??
                throw new ArgumentException($"{type} missing Workflow attribute");
            // Find the one and only run attribute and fail if there are multiple. Note, this has to
            // be declared on this class, so superclass versions cannot have it.
            MethodInfo? runMethod = null;
            foreach (var method in type.GetMethods())
            {
                var runAttr = method.GetCustomAttribute<WorkflowRunAttribute>(false);
                if (runAttr != null)
                {
                    if (method.DeclaringType != type)
                    {
                        throw new ArgumentException($"WorkflowRun on {method} must be declared on {type}, not inherited from {method.DeclaringType}");
                    }
                    else if (runMethod != null)
                    {
                        throw new ArgumentException($"WorkflowRun on {method} and {runMethod}");
                    }
                    else if (!method.IsPublic)
                    {
                        throw new ArgumentException($"WorkflowRun on {method} is not public");
                    }
                    runMethod = method;
                }
            }
            if (runMethod == null)
            {
                throw new ArgumentException($"{type} does not have a WorkflowRun method");
            }
            // Use type name by default
            var name = attr.Name;
            if (name == null)
            {
                // If type is an interface and name has a leading I followed by another capital, trim it
                // off
                name = type.Name;
                if (type.IsInterface && name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]))
                {
                    name = name.Substring(1);
                }
            }
            return new WorkflowDefinition(
                Name: name,
                Type: type,
                RunMethod: runMethod);
        }
    }
}