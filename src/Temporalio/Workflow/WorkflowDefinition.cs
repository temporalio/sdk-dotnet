
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Temporalio.Workflow
{
    internal record WorkflowDefinition(
        string Name,
        Type Class,
        MethodInfo RunMethod)
        // TODO(cretz):
        // bool ArgsInConstructor = false,
        // IReadOnlyDictionary<string, MethodInfo>? SignalMethods = null,
        // MethodInfo? DynamicSignal = null,
        // IReadOnlyDictionary<string, MethodInfo>? QueryMethods = null,
        // MethodInfo? DynamicQuery = null)
    {
        private static readonly ConcurrentDictionary<Type, WorkflowDefinition> definitionsByType = new();

        public static WorkflowDefinition FromType(Type type) {
            return definitionsByType.GetOrAdd(type, CreateFromType);
        }

        public static WorkflowDefinition FromRunMethod(MethodInfo runMethod) {
            if (runMethod.GetCustomAttribute<WorkflowRunAttribute>() == null) {
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
                if (runAttr != null) {
                    if (method.DeclaringType != type)
                    {
                        throw new ArgumentException($"WorkflowRun on {method} must be declared on {type}, not inherited from {method.DeclaringType}");
                    } else if (runMethod != null) {
                        throw new ArgumentException($"WorkflowRun on {method} and {runMethod}");
                    } else if (!method.IsPublic) {
                        throw new ArgumentException($"WorkflowRun on {method} is not public");
                    }
                    runMethod = method;
                }
            }
            if (runMethod == null) {
                throw new ArgumentException($"{type} does not have a WorkflowRun method");
            }
            // Use type name by default
            var name = attr.Name;
            if (name == null) {
                // If type is an interface and name has a leading I followed by another capital, trim it
                // off
                name = type.Name;
                if (type.IsInterface && name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1])) {
                    name = name[1..];
                }
            }
            return new WorkflowDefinition(
                Name: name,
                Class: type,
                RunMethod: runMethod);
        }
    }
}