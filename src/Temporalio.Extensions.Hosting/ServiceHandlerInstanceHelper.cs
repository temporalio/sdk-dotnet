using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NexusRpc;
using NexusRpc.Handlers;

namespace Temporalio.Extensions.Hosting
{
    /// <summary>
    /// Helper for contructing <see cref="ServiceHandlerInstance"/>.
    /// </summary>
    /// <remarks>
    /// This is internal and should be moved to NexusRpc in the future.
    /// </remarks>
    internal static class ServiceHandlerInstanceHelper
    {
        /// <summary>
        /// Create a service handler instance from the given service handler type and handler factory.
        /// </summary>
        /// <param name="serviceHandlerType">The type of the Nexus service handler.</param>
        /// <param name="handlerFactory">A factory that converts method information into an operation handler.</param>
        /// <returns>A <see cref="ServiceHandlerInstance"/> for the given <paramref name="serviceHandlerType"/> type.</returns>
        public static ServiceHandlerInstance FromType(Type serviceHandlerType, Func<MethodInfo, IOperationHandler<object?, object?>> handlerFactory)
        {
            var serviceDef = GetServiceDefinition(serviceHandlerType);

            return new ServiceHandlerInstance(
                serviceDef,
                CreateHandlers(
                    serviceDef,
                    serviceHandlerType,
                    handlerFactory));
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
        /// Collects all public methods from the given type and its base types recursively.
        /// </summary>
        /// <param name="serviceHandlerType">The type of the Nexus service handler.</param>
        /// <param name="methods">The list to which discovered methods are added.</param>
        private static void CollectTypeMethods(Type serviceHandlerType, List<MethodInfo> methods)
        {
            // Add all declared public static/instance methods that do not already have one like
            // it present
            foreach (var method in serviceHandlerType.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
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

        /// <summary>
        /// Validates and adds an operation handler created from the given operation handler method.
        /// </summary>
        /// <param name="serviceDef">A <see cref="ServiceDefinition"/> for the given service handler type.</param>
        /// <param name="method">The method from which an operation hander is created.</param>
        /// <param name="handlerFactory">A factory that creates an operation handler for a given method.</param>
        /// <param name="opHandlers">The mapping of operation names to operation handlers.</param>
        private static void AddOperationHandler(
            ServiceDefinition serviceDef,
            MethodInfo method,
            Func<MethodInfo, IOperationHandler<object?, object?>> handlerFactory,
            Dictionary<string, IOperationHandler<object?, object?>> opHandlers)
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

            // Find definition by the method name
            var opDef = serviceDef.Operations.Values.FirstOrDefault(o => o.MethodInfo?.Name == method.Name) ??
                throw new ArgumentException("No matching NexusOperation on the service interface");

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

            // Confirm not present already
            if (opHandlers.ContainsKey(opDef.Name))
            {
                throw new ArgumentException($"Duplicate operation handler named ${opDef.Name}");
            }

            opHandlers[opDef.Name] = handlerFactory(method);
        }

        /// <summary>
        /// Creates a mapping of operation names to operation handlers for the given service handler type.
        /// </summary>
        /// <param name="serviceDef">A <see cref="ServiceDefinition"/> for the given service handler type.</param>
        /// <param name="serviceHandlerType">The type of the Nexus service handler.</param>
        /// <param name="handlerFactory">A factory that creates an operation handler for a given method.</param>
        /// <returns>A mapping of operation names to operation handlers.</returns>
        private static Dictionary<string, IOperationHandler<object?, object?>> CreateHandlers(
            ServiceDefinition serviceDef,
            Type serviceHandlerType,
            Func<MethodInfo, IOperationHandler<object?, object?>> handlerFactory)
        {
            // Collect all methods recursively
            var methods = new List<MethodInfo>();
            CollectTypeMethods(serviceHandlerType, methods);

            // Collect handlers from the method list
            var opHandlers = new Dictionary<string, IOperationHandler<object?, object?>>();
            foreach (var method in methods)
            {
                // Only care about ones with operation attribute
                if (method.GetCustomAttribute<NexusOperationHandlerAttribute>() == null)
                {
                    continue;
                }

                try
                {
                    AddOperationHandler(serviceDef, method, handlerFactory, opHandlers);
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        $"Failed obtaining operation handler from {method.Name}", e);
                }
            }

            return opHandlers;
        }
    }
}