using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NexusRpc.Handlers;
using Temporalio.Activities;
using Temporalio.Nexus;

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
            async Task<object?> InvokerAsync(object?[] args)
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
            return ActivityDefinition.Create(method, InvokerAsync);
        }

        /// <summary>
        /// Create <see cref="ServiceHandlerInstance"/> for the given nexus-attributed service handler type.
        /// If a service handler method is non-static, this will use the service provider to get the service
        /// instance to call the method on. Both <see cref="NexusOperationHandlerAttribute"/> factory
        /// methods and <see cref="TemporalOperationAttribute"/> methods are supported; each operation
        /// resolves a fresh service instance from a dependency-injection scope on every start and
        /// cancel call.
        /// </summary>
        /// <param name="provider">Service provider for creating the service instance if the
        /// method is non-static.</param>
        /// <param name="serviceHandlerType">The type of the Nexus service handler.</param>
        /// <returns>Created <see cref="ServiceHandlerInstance"/>.</returns>
        internal static ServiceHandlerInstance CreateNexusServiceHandlerInstance(
            this IServiceProvider provider,
            Type serviceHandlerType) =>
            ServiceHandlerInstanceHelper.FromType(
                serviceHandlerType,
                (method, opDef) =>
                {
                    // [NexusOperationHandler]: the method is a factory that returns the handler.
                    if (method.GetCustomAttribute<NexusOperationHandlerAttribute>() != null)
                    {
                        ServiceHandlerInstanceHelper.ValidateNexusOperationHandler(opDef, method);
                        return new ScopedOperationHandler(
                            serviceHandlerType,
                            resolveInstance: !method.IsStatic,
                            provider,
                            instance => InvokeNexusOperationHandlerFactory(method, instance));
                    }

                    // [TemporalOperation]: the method itself is the start handler. Core validates it
                    // and returns a factory that binds a resolved instance into the handler.
                    if (method.GetCustomAttribute<TemporalOperationAttribute>() != null)
                    {
                        var handlerFactory =
                            TemporalOperationMethodExtension.CreateInstanceHandlerFactory(method, opDef);
                        return new ScopedOperationHandler(
                            serviceHandlerType,
                            resolveInstance: true,
                            provider,
                            instance => handlerFactory(instance!));
                    }

                    return null;
                });

        /// <summary>
        /// Invoke a <see cref="NexusOperationHandlerAttribute"/> factory method to obtain its
        /// operation handler, unwrapping the target-invocation exception on failure.
        /// </summary>
        private static IOperationHandler<object?, object?> InvokeNexusOperationHandlerFactory(
            MethodInfo method, object? instance)
        {
            object handler;
            try
            {
                handler = method.Invoke(instance, null) ??
                    throw new ArgumentException("Operation handler was null");
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
            return OperationHandler.WrapAsGenericHandler(handler, method.ReturnType);
        }

        /// <summary>
        /// An operation handler that defers the resolution of the Nexus service handler and the
        /// construction of the underlying operation handler to be within a service scope. A fresh
        /// scope (and, unless the operation is static, a fresh service instance) is created for
        /// every start and cancel call, giving the operation the same dependency-injection behavior
        /// as activities.
        /// </summary>
        private sealed class ScopedOperationHandler :
            IOperationHandler<object?, object?>
        {
            private readonly Type serviceHandlerType;
            private readonly bool resolveInstance;
            private readonly IServiceProvider serviceProvider;
            private readonly Func<object?, IOperationHandler<object?, object?>> handlerBuilder;

            public ScopedOperationHandler(
                Type serviceHandlerType,
                bool resolveInstance,
                IServiceProvider serviceProvider,
                Func<object?, IOperationHandler<object?, object?>> handlerBuilder)
            {
                this.serviceHandlerType = serviceHandlerType;
                this.resolveInstance = resolveInstance;
                this.serviceProvider = serviceProvider;
                this.handlerBuilder = handlerBuilder;
            }

            public async Task<OperationStartResult<object?>> StartAsync(OperationStartContext context, object? input) =>
                await InvokeWithScopeAsync(handler => handler.StartAsync(context, input)).ConfigureAwait(false);

            public async Task CancelAsync(OperationCancelContext context) =>
                await InvokeWithScopeAsync(handler => handler.CancelAsync(context).ContinueWith(
                    _ => ValueTuple.Create(),
                    default,
                    TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnRanToCompletion,
                    TaskScheduler.Current)).ConfigureAwait(false);

            private async Task<T> InvokeWithScopeAsync<T>(Func<IOperationHandler<object?, object?>, Task<T>> handlerInvoker)
            {
#if NET6_0_OR_GREATER
                AsyncServiceScope scope = this.serviceProvider.CreateAsyncScope();
#else
                IServiceScope scope = this.serviceProvider.CreateScope();
#endif

                try
                {
                    // Resolve the service instance for non-static operations, then build the handler
                    // bound to it. The handler (and any user code it runs) executes within this scope.
                    var instance = this.resolveInstance
                        ? scope.ServiceProvider.GetRequiredService(this.serviceHandlerType)
                        : null;
                    var handler = this.handlerBuilder(instance);
                    return await handlerInvoker(handler).ConfigureAwait(false);
                }
                finally
                {
#if NET6_0_OR_GREATER
                    await scope.DisposeAsync().ConfigureAwait(false);
#else
                    scope.Dispose();
#endif
                }
            }
        }
    }
}