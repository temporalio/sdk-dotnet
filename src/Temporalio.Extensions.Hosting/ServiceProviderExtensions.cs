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
                Select(method => provider.CreateTemporalActivityDefinition(type, method)).
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
            this IServiceProvider provider,
            Type instanceType,
            MethodInfo method)
        {
            // Invoker can be async (i.e. returns Task<object?>)
            async Task<object?> Invoker(object?[] args)
            {
                // Wrap in a scope if scope doesn't already exist. Keep track of whether we created
                // it so we can dispose of it.
                var scope = ActivityScope.ServiceScope;
                var createdScopeOurselves = scope == null;
                if (scope == null)
                {
#if NET6_0_OR_GREATER
                    scope = provider.CreateAsyncScope();
#else
                    scope = provider.CreateScope();
#endif
                    ActivityScope.ServiceScope = scope;
                }

                // Run
                try
                {
                    object? result;
                    try
                    {
                        // Create the instance if not static and not already created
                        var instance = method.IsStatic
                            ? null
                            : ActivityScope.ScopedInstance ?? scope.ServiceProvider.GetRequiredService(instanceType);
                        ActivityScope.ScopedInstance = instance;

                        result = method.Invoke(instance, args);
                    }
                    catch (TargetInvocationException e)
                    {
#if NET6_0_OR_GREATER
                        ExceptionDispatchInfo.Capture(e.InnerException!).Throw();
#else
                        ExceptionDispatchInfo.Capture(e.InnerException).Throw();
#endif
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
                finally
                {
                    // Dispose of scope if we created it
                    if (createdScopeOurselves)
                    {
#if NET6_0_OR_GREATER
                        if (scope is AsyncServiceScope asyncScope)
                        {
                            await asyncScope.DisposeAsync().ConfigureAwait(false);
                        }
                        else
                        {
                            scope.Dispose();
                        }
#else
                        scope.Dispose();
#endif
                    }
                    ActivityScope.ServiceScope = null;
                    ActivityScope.ScopedInstance = null;
                }
            }
            return ActivityDefinition.Create(method, Invoker);
        }
    }
}