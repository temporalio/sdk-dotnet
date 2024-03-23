using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Activities;

namespace Temporalio.Extensions.Hosting
{
    /// <summary>
    /// Temporal extension methods for <see cref="IServiceProvider" />.
    /// </summary>
    public static class ServiceProviderExtensions
    {
        /// <summary>
        /// Create activity definitions for every activity-attributed method on the given type. For
        /// non-static methods, this will use the service provider to get the instance to call the
        /// method on.
        /// </summary>
        /// <typeparam name="T">Type to create activity definitions from.</typeparam>
        /// <param name="provider">Service provider for creating the instance for non-static
        /// activities.</param>
        /// <returns>Collection of activity definitions.</returns>
        public static IReadOnlyCollection<ActivityDefinition> CreateTemporalActivityDefinitions<T>(
            this IServiceProvider provider) =>
            provider.CreateTemporalActivityDefinitions(typeof(T));

        /// <summary>
        /// Create activity definitions for every activity-attributed method on the given type. For
        /// non-static methods, this will use the service provider to get the instance to call the
        /// method on.
        /// </summary>
        /// <param name="provider">Service provider for creating the instance for non-static
        /// activities.</param>
        /// <param name="type">Type to create activity definitions from.</param>
        /// <returns>Collection of activity definitions.</returns>
        public static IReadOnlyCollection<ActivityDefinition> CreateTemporalActivityDefinitions(
            this IServiceProvider provider, Type type) =>
            type.
                GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).
                Where(method => method.IsDefined(typeof(ActivityAttribute))).
#if NET6_0_OR_GREATER
                Select(method => provider.AsyncCreateTemporalActivityDefinition(type, method)).
#else
                Select(method => provider.CreateTemporalActivityDefinition(type, method)).
#endif
                ToList();

        /// <summary>
        /// Create activity definition for the given activity-attributed method on the given
        /// instance type. If the method is non-static, this will use the service provider to get
        /// the instance to call the method on.
        /// </summary>
        /// <param name="provider">Service provider for creating the instance if the method is
        /// non-static.</param>
        /// <param name="instanceType">Type of the instance.</param>
        /// <param name="method">Method to create activity definition from.</param>
        /// <returns>Created definition.</returns>
        public static ActivityDefinition CreateTemporalActivityDefinition(
            this IServiceProvider provider, Type instanceType, MethodInfo method)
        {
            // Invoker can be async (i.e. returns Task<object?>)
            Func<object?[], Task<object?>> invoker = async args =>
            {
                // Wrap in a scope (even for statics to keep logic simple)
                using (var scope = provider.CreateScope())
                {
                    object? result = null;
                    try
                    {
                        // Invoke static or non-static
                        var instance = method.IsStatic ? null : scope.ServiceProvider.GetRequiredService(instanceType);
                        result = method.Invoke(instance, args);
                    }
                    catch (TargetInvocationException e)
                    {
                        ExceptionDispatchInfo.Capture(e.InnerException!).Throw();
                        // Unreachable
                        throw new InvalidOperationException("Unreachable");
                    }
                    // In order to make sure the scope lasts the life of the activity, we need to
                    // wait on the task if it's a task
                    if (result is Task resultTask)
                    {
                        await resultTask.ConfigureAwait(false);
                        // We have to use reflection to extract value if it's a Task<>
                        var resultTaskType = resultTask.GetType();
                        if (resultTaskType.IsGenericType)
                        {
                            result = resultTaskType.GetProperty("Result")!.GetValue(resultTask);
                        }
                        else
                        {
                            result = ValueTuple.Create();
                        }
                    }
                    return result;
                }
            };
            return ActivityDefinition.Create(method, invoker);
        }

#if NET6_0_OR_GREATER
        /// <summary>
        /// Create activity definition for the given activity-attributed method on the given
        /// instance type. If the method is non-static, this will use the service provider to get
        /// the instance to call the method on.
        /// </summary>
        /// <param name="provider">Service provider for creating the instance if the method is
        /// non-static.</param>
        /// <param name="instanceType">Type of the instance.</param>
        /// <param name="method">Method to create activity definition from.</param>
        /// <returns>Created definition.</returns>
        public static ActivityDefinition AsyncCreateTemporalActivityDefinition(
            this IServiceProvider provider,
            Type instanceType,
            MethodInfo method)
        {
            // Invoker can be async (i.e. returns Task<object?>)
            async Task<object?> Invoker(object?[] args)
            {
                // Wrap in a scope (even for statics to keep logic simple)
#pragma warning disable CA2007 // Invalid for AsyncServiceScope, ServiceProvider is not accessible via ConfiguredAsyncDisposable object type.
                await using var scope = provider.CreateAsyncScope();
#pragma warning restore CA2007

                object? result = null;
                try
                {
                    // Invoke static or non-static
                    var instance = method.IsStatic
                        ? null
                        : scope.ServiceProvider.GetRequiredService(instanceType);

                    result = method.Invoke(instance, args);
                }
                catch (TargetInvocationException e)
                {
                    ExceptionDispatchInfo.Capture(e.InnerException!).Throw();
                }

                // In order to make sure the scope lasts the life of the activity, we need to
                // wait on the task if it's a task
                if (result is Task resultTask)
                {
                    await resultTask.ConfigureAwait(false);
                    // We have to use reflection to extract value if it's a Task<>
                    var resultTaskType = resultTask.GetType();
                    if (resultTaskType.IsGenericType)
                    {
                        result = resultTaskType.GetProperty("Result")!.GetValue(resultTask);
                    }
                    else
                    {
                        result = ValueTuple.Create();
                    }
                }
                return result!;
            }
            return ActivityDefinition.Create(method, Invoker);
        }
#endif
    }
}