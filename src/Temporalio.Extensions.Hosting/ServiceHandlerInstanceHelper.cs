using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NexusRpc;
using NexusRpc.Handlers;

namespace Temporalio.Extensions.Hosting
{
    /// <summary>
    /// Helper for constructing <see cref="ServiceHandlerInstance"/>.
    /// </summary>
    /// <remarks>
    /// This is internal and should be moved to NexusRpc in the future.
    /// </remarks>
    internal static class ServiceHandlerInstanceHelper
    {
        /// <summary>
        /// Create a service handler instance from the given service handler type. Each method on the
        /// type and its base types that maps by name to an operation on the service is offered to
        /// <paramref name="operationFactory"/>, which decides whether the method is a handler and, if
        /// so, returns it. The operation the handler is registered under is the one matched here by
        /// method name, the same way NexusRpc matches handlers.
        /// </summary>
        /// <param name="serviceHandlerType">The type of the Nexus service handler.</param>
        /// <param name="operationFactory">Factory invoked for each method that maps to an operation,
        /// with that operation's definition. Returns the handler when the method is recognized, or
        /// <c>null</c> to skip it.</param>
        /// <returns>A <see cref="ServiceHandlerInstance"/> for the given
        /// <paramref name="serviceHandlerType"/> type.</returns>
        internal static ServiceHandlerInstance FromType(
            Type serviceHandlerType,
            Func<MethodInfo, OperationDefinition,
                IOperationHandler<object?, object?>?> operationFactory)
        {
            var serviceDef = GetServiceDefinition(serviceHandlerType);

            // Collect all methods recursively
            var methods = new List<MethodInfo>();
            CollectTypeMethods(serviceHandlerType, methods);

            // Ask the factory for a handler per method that maps to an operation, collecting the
            // ones it recognizes.
            var opHandlers = new Dictionary<string, IOperationHandler<object?, object?>>();
            foreach (var method in methods)
            {
                var opDef = serviceDef.Operations.Values
                    .FirstOrDefault(o => o.MethodInfo?.Name == method.Name);
                if (opDef == null)
                {
                    // A built-in [NexusOperationHandler] method that maps to no operation is an
                    // error, mirroring NexusRpc's ServiceHandlerInstance.FromInstance. Extension
                    // attributes such as [TemporalOperation] are only consulted for name-matched
                    // operations, so a method carrying only such an attribute is silently skipped.
                    if (method.GetCustomAttribute<NexusOperationHandlerAttribute>() != null)
                    {
                        throw new ArgumentException(
                            $"Failed obtaining operation handler from {method.Name}",
                            new ArgumentException(
                                "No matching NexusOperation on the service interface"));
                    }
                    continue;
                }
                IOperationHandler<object?, object?>? handler;
                try
                {
                    handler = operationFactory(method, opDef);
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        $"Failed obtaining operation handler from {method.Name}", e);
                }
                if (handler == null)
                {
                    continue;
                }
                if (opHandlers.ContainsKey(opDef.Name))
                {
                    throw new ArgumentException($"Duplicate operation handler named {opDef.Name}");
                }
                opHandlers[opDef.Name] = handler;
            }

            return new ServiceHandlerInstance(serviceDef, opHandlers);
        }

        /// <summary>
        /// Validate a <see cref="NexusOperationHandlerAttribute"/> factory method against the
        /// operation it handles.
        /// </summary>
        /// <param name="opDef">Operation the method handles, matched by method name.</param>
        /// <param name="method">The factory method to validate.</param>
        internal static void ValidateNexusOperationHandler(
            OperationDefinition opDef, MethodInfo method)
        {
            // Validate
            if (method.GetParameters().Length != 0)
            {
                throw new ArgumentException("Cannot have parameters");
            }
            if (method.ContainsGenericParameters)
            {
                throw new ArgumentException("Cannot be generic");
            }
            if (!method.IsPublic)
            {
                throw new ArgumentException("Must be public");
            }

            // Check return
            var goodReturn = false;
            if (method.ReturnType.IsGenericType &&
                method.ReturnType.GetGenericTypeDefinition() == typeof(IOperationHandler<,>))
            {
                var args = method.ReturnType.GetGenericArguments();
                goodReturn = args.Length == 2 &&
                    NoValue.NormalizeVoidType(args[0]) == opDef.InputType &&
                    NoValue.NormalizeVoidType(args[1]) == opDef.OutputType;
            }
            if (!goodReturn)
            {
                var inType = opDef.InputType == typeof(void) ? typeof(NoValue) : opDef.InputType;
                var outType = opDef.OutputType == typeof(void) ? typeof(NoValue) : opDef.OutputType;
                throw new ArgumentException(
                    $"Expected return type of IOperationHandler<{inType.Name}, {outType.Name}>");
            }
        }

        /// <summary>
        /// Creates a <see cref="ServiceDefinition"/> for the given service handler type.
        /// </summary>
        /// <param name="serviceHandlerType">The type of the Nexus service handler.</param>
        /// <returns>A <see cref="ServiceDefinition"/> for the given  <paramref name="serviceHandlerType"/> type.</returns>
        private static ServiceDefinition GetServiceDefinition(Type serviceHandlerType)
        {
            // Make sure the attribute is on the declaring type of the instance
            var handlerAttr = serviceHandlerType.GetCustomAttribute<NexusServiceHandlerAttribute>() ??
                throw new ArgumentException("Missing NexusServiceHandler attribute");
            return ServiceDefinition.FromType(handlerAttr.ServiceType);
        }

        /// <summary>
        /// Collects all methods (public and non-public) from the given type and its base types
        /// recursively.
        /// </summary>
        /// <param name="serviceHandlerType">The type of the Nexus service handler.</param>
        /// <param name="methods">The list to which discovered methods are added.</param>
        private static void CollectTypeMethods(Type serviceHandlerType, List<MethodInfo> methods)
        {
            // Add all declared static/instance methods (public + non-public) that do not already
            // have one like it present. Non-public methods are included so the operation factory can
            // produce a clear error when an operation attribute is applied to a non-public method,
            // matching NexusRpc's ServiceHandlerInstance.FromInstance behavior.
            foreach (var method in serviceHandlerType.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                // Only add if there isn't already one that matches the base definition
                var baseDef = method.GetBaseDefinition();
                if (!methods.Any(m => baseDef == m.GetBaseDefinition()))
                {
                    methods.Add(method);
                }
            }
            if (serviceHandlerType.BaseType is { } baseType)
            {
                CollectTypeMethods(baseType, methods);
            }
        }
    }
}
