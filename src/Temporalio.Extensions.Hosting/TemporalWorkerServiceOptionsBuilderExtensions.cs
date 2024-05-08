using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Temporalio.Extensions.Hosting
{
    /// <summary>
    /// Extension methods for <see cref="ITemporalWorkerServiceOptionsBuilder" /> to configure
    /// worker services.
    /// </summary>
    public static class TemporalWorkerServiceOptionsBuilderExtensions
    {
        /// <summary>
        /// Register the given type as a singleton if not already done and all activities of the
        /// given type to the worker. Basically
        /// <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton{TService}(IServiceCollection)" />
        /// +
        /// <see cref="ApplyTemporalActivities{T}(ITemporalWorkerServiceOptionsBuilder)" />.
        /// </summary>
        /// <typeparam name="T">Type to register activities for.</typeparam>
        /// <param name="builder">Builder to use.</param>
        /// <returns>Same builder instance.</returns>
        public static ITemporalWorkerServiceOptionsBuilder AddSingletonActivities<T>(
            this ITemporalWorkerServiceOptionsBuilder builder) => builder.AddSingletonActivities(typeof(T));

        /// <summary>
        /// Register the given type as a singleton if not already done and all activities of the
        /// given type to the worker. Basically
        /// <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton(IServiceCollection, Type)" />
        /// +
        /// <see cref="ApplyTemporalActivities(ITemporalWorkerServiceOptionsBuilder, Type)" />.
        /// </summary>
        /// <param name="builder">Builder to use.</param>
        /// <param name="type">Type to register activities for.</param>
        /// <returns>Same builder instance.</returns>
        public static ITemporalWorkerServiceOptionsBuilder AddSingletonActivities(
            this ITemporalWorkerServiceOptionsBuilder builder, Type type)
        {
            builder.Services.TryAddSingleton(type);
            return builder.ApplyTemporalActivities(type);
        }

        /// <summary>
        /// Register the given type as scoped if not already done and all activities of the given
        /// type to the worker. Basically
        /// <see cref="ServiceCollectionDescriptorExtensions.TryAddScoped{TService}(IServiceCollection)" />
        /// +
        /// <see cref="ApplyTemporalActivities{T}(ITemporalWorkerServiceOptionsBuilder)" />.
        /// </summary>
        /// <typeparam name="T">Type to register activities for.</typeparam>
        /// <param name="builder">Builder to use.</param>
        /// <returns>Same builder instance.</returns>
        public static ITemporalWorkerServiceOptionsBuilder AddScopedActivities<T>(
            this ITemporalWorkerServiceOptionsBuilder builder) => builder.AddScopedActivities(typeof(T));

        /// <summary>
        /// Register the given type as a scoped if not already done and all activities of the given
        /// type to the worker. Basically
        /// <see cref="ServiceCollectionDescriptorExtensions.TryAddScoped(IServiceCollection, Type)" />
        /// +
        /// <see cref="ApplyTemporalActivities(ITemporalWorkerServiceOptionsBuilder, Type)" />.
        /// </summary>
        /// <param name="builder">Builder to use.</param>
        /// <param name="type">Type to register activities for.</param>
        /// <returns>Same builder instance.</returns>
        public static ITemporalWorkerServiceOptionsBuilder AddScopedActivities(
            this ITemporalWorkerServiceOptionsBuilder builder, Type type)
        {
            builder.Services.TryAddScoped(type);
            return builder.ApplyTemporalActivities(type);
        }

        /// <summary>
        /// Register the given type as transient if not already done and all activities of the given
        /// type to the worker. Basically
        /// <see cref="ServiceCollectionDescriptorExtensions.TryAddTransient{TService}(IServiceCollection)" />
        /// +
        /// <see cref="ApplyTemporalActivities{T}(ITemporalWorkerServiceOptionsBuilder)" />.
        /// </summary>
        /// <typeparam name="T">Type to register activities for.</typeparam>
        /// <param name="builder">Builder to use.</param>
        /// <returns>Same builder instance.</returns>
        public static ITemporalWorkerServiceOptionsBuilder AddTransientActivities<T>(
            this ITemporalWorkerServiceOptionsBuilder builder) => builder.AddTransientActivities(typeof(T));

        /// <summary>
        /// Register the given type as transient if not already done and all activities of the given
        /// type to the worker. Basically
        /// <see cref="ServiceCollectionDescriptorExtensions.TryAddTransient(IServiceCollection, Type)" />
        /// +
        /// <see cref="ApplyTemporalActivities(ITemporalWorkerServiceOptionsBuilder, Type)" />.
        /// </summary>
        /// <param name="builder">Builder to use.</param>
        /// <param name="type">Type to register activities for.</param>
        /// <returns>Same builder instance.</returns>
        public static ITemporalWorkerServiceOptionsBuilder AddTransientActivities(
            this ITemporalWorkerServiceOptionsBuilder builder, Type type)
        {
            builder.Services.TryAddTransient(type);
            return builder.ApplyTemporalActivities(type);
        }

        /// <summary>
        /// Register the given type's activities. The activity methods must all be static.
        /// </summary>
        /// <typeparam name="T">Type to register activities for.</typeparam>
        /// <param name="builder">Builder to use.</param>
        /// <returns>Same builder instance.</returns>
        public static ITemporalWorkerServiceOptionsBuilder AddStaticActivities<T>(
            this ITemporalWorkerServiceOptionsBuilder builder) => builder.AddStaticActivities(typeof(T));

        /// <summary>
        /// Register the given type's activities. The activity methods must all be static.
        /// </summary>
        /// <param name="builder">Builder to use.</param>
        /// <param name="type">Type to register activities for.</param>
        /// <returns>Same builder instance.</returns>
        public static ITemporalWorkerServiceOptionsBuilder AddStaticActivities(
            this ITemporalWorkerServiceOptionsBuilder builder, Type type) =>
            builder.ConfigureOptions(options => options.AddAllActivities(type, null));

        /// <summary>
        /// Register the given type's activities with an existing instance. The given instance will
        /// be used to invoke non-static activity methods.
        /// </summary>
        /// <typeparam name="T">Type to register activities for.</typeparam>
        /// <param name="builder">Builder to use.</param>
        /// <param name="instance">Instance to use.</param>
        /// <returns>Same builder instance.</returns>
        public static ITemporalWorkerServiceOptionsBuilder AddActivitiesInstance<T>(
            this ITemporalWorkerServiceOptionsBuilder builder, T instance) =>
            builder.AddActivitiesInstance(typeof(T), instance!);

        /// <summary>
        /// Register the given type's activities with an existing instance. The given instance will
        /// be used to invoke non-static activity methods.
        /// </summary>
        /// <param name="builder">Builder to use.</param>
        /// <param name="type">Type to register activities for.</param>
        /// <param name="instance">Instance to use.</param>
        /// <returns>Same builder instance.</returns>
        public static ITemporalWorkerServiceOptionsBuilder AddActivitiesInstance(
            this ITemporalWorkerServiceOptionsBuilder builder, Type type, object instance) =>
            builder.ConfigureOptions(options => options.AddAllActivities(type, instance));

        /// <summary>
        /// Add the given workflow type as a workflow on this worker service.
        /// </summary>
        /// <typeparam name="T">Workflow type.</typeparam>
        /// <param name="builder">Builder to use.</param>
        /// <returns>Same builder instance.</returns>
        public static ITemporalWorkerServiceOptionsBuilder AddWorkflow<T>(
            this ITemporalWorkerServiceOptionsBuilder builder) => builder.AddWorkflow(typeof(T));

        /// <summary>
        /// Add the given workflow type as a workflow on this worker service.
        /// </summary>
        /// <param name="builder">Builder to use.</param>
        /// <param name="type">Workflow type.</param>
        /// <returns>Same builder instance.</returns>
        public static ITemporalWorkerServiceOptionsBuilder AddWorkflow(
            this ITemporalWorkerServiceOptionsBuilder builder, Type type) =>
            builder.ConfigureOptions(options => options.AddWorkflow(type));

        /// <summary>
        /// Get an options builder to configure worker service options.
        /// </summary>
        /// <param name="builder">Builder to use.</param>
        /// <param name="disallowDuplicates">If true, will fail if options already registered for
        /// this builder's task queue and build ID.</param>
        /// <returns>Options builder.</returns>
        public static OptionsBuilder<TemporalWorkerServiceOptions> ConfigureOptions(
            this ITemporalWorkerServiceOptionsBuilder builder,
            bool disallowDuplicates = false)
        {
            // To ensure the user isn't accidentally registering a duplicate task queue + build ID
            // worker, we check here that there aren't duplicate options
            var optionsName = TemporalWorkerServiceOptions.GetUniqueOptionsName(
                    builder.TaskQueue, builder.BuildId);
            if (disallowDuplicates)
            {
                var any = builder.Services.Any(s =>
                {
                    // Since https://github.com/dotnet/runtime/pull/87183 in 8.0.0+ of
                    // Microsoft.Extensions.DependencyInjection.Abstractions, simply accessing this
                    // property on a service can throw an exception if the service is "keyed".
                    // Knowing whether a service is keyed is only exposed programmatically in that
                    // newer version and we don't want to depend on that newer version. And we know
                    // that we never make our options keyed, so we can just swallow the exception.
                    try
                    {
                        return s.ImplementationInstance is ConfigureNamedOptions<TemporalWorkerServiceOptions> instance &&
                            instance.Name == optionsName;
                    }
                    catch (InvalidOperationException)
                    {
                        return false;
                    }
                });
                if (any)
                {
                    throw new InvalidOperationException(
                        $"Worker service for task queue '{builder.TaskQueue}' and build ID '{builder.BuildId ?? "<unset>"}' already on collection");
                }
            }

            return builder.Services.AddOptions<TemporalWorkerServiceOptions>(optionsName);
        }

        /// <summary>
        /// Configure worker service options using an action.
        /// </summary>
        /// <param name="builder">Builder to use.</param>
        /// <param name="configure">Configuration action.</param>
        /// <param name="disallowDuplicates">If true, will fail if options already registered for
        /// this builder's task queue and build ID.</param>
        /// <returns>Same builder instance.</returns>
        public static ITemporalWorkerServiceOptionsBuilder ConfigureOptions(
            this ITemporalWorkerServiceOptionsBuilder builder,
            Action<TemporalWorkerServiceOptions> configure,
            bool disallowDuplicates = false)
        {
            builder.ConfigureOptions(disallowDuplicates).Configure(configure);
            return builder;
        }

        /// <summary>
        /// Register the given type's activities. This uses the service provider to create the
        /// instance for non-static methods, but does not register the type/instance with the
        /// service collection.
        /// </summary>
        /// <typeparam name="T">Type to register activities for.</typeparam>
        /// <param name="builder">Builder to use.</param>
        /// <returns>Same builder instance.</returns>
        public static ITemporalWorkerServiceOptionsBuilder ApplyTemporalActivities<T>(
            this ITemporalWorkerServiceOptionsBuilder builder) =>
            builder.ApplyTemporalActivities(typeof(T));

        /// <summary>
        /// Register the given type's activities. This uses the service provider to create the
        /// instance for non-static methods, but does not register the type/instance with the
        /// service collection.
        /// </summary>
        /// <param name="builder">Builder to use.</param>
        /// <param name="type">Type to register activities for.</param>
        /// <returns>Same builder instance.</returns>
        public static ITemporalWorkerServiceOptionsBuilder ApplyTemporalActivities(
            this ITemporalWorkerServiceOptionsBuilder builder, Type type)
        {
            builder.ConfigureOptions().PostConfigure<IServiceProvider>((options, provider) =>
            {
                foreach (var defn in provider.CreateTemporalActivityDefinitions(type))
                {
                    options.AddActivity(defn);
                }
            });
            return builder;
        }
    }
}