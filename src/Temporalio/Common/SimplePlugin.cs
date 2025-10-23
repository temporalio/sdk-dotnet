using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Worker;

namespace Temporalio.Common
{
    public class SimplePlugin: ITemporalClientPlugin, ITemporalWorkerPlugin
    {
        private readonly SimplePluginOptions pluginOptions;

        public string Name { get; }

        public SimplePlugin(
            string name,
            SimplePluginOptions options)
        {
            Name = name;
            pluginOptions = options;
        }

        public void ConfigureClient(TemporalClientOptions options)
        {
            options.DataConverter = Resolve(options.DataConverter, pluginOptions.DataConverterOption);
            options.Interceptors = ResolveAppend(options.Interceptors, pluginOptions.ClientInterceptorsOption);
        }

        public Task<TemporalConnection> TemporalConnectAsync(TemporalClientConnectOptions options, Func<TemporalClientConnectOptions, Task<TemporalConnection>> continuation) => throw new NotImplementedException();

        public void ConfigureWorker(TemporalWorkerOptions options)
        {
            DoAppend(options.Activities, pluginOptions.Activities);
            DoAppend(options.Workflows, pluginOptions.Workflows);
            DoAppend(options.NexusServices, pluginOptions.NexusServices);
            options.Interceptors = ResolveAppend(options.Interceptors, pluginOptions.WorkerInterceptorsOption);
            options.WorkflowFailureExceptionTypes = ResolveAppend(options.WorkflowFailureExceptionTypes, pluginOptions.WorkflowFailureExceptionTypesOption);
        }

        public Task RunWorker(TemporalWorker worker, Func<TemporalWorker, Task> continuation)
        {
            if (pluginOptions.RunContext != null)
            {
                return pluginOptions.RunContext(() => continuation(worker) as Task);
            }
            return continuation(worker);
        }

        public void ConfigureReplayer(WorkflowReplayerOptions options)
        {
            options.DataConverter = Resolve(options.DataConverter, pluginOptions.DataConverterOption);
            DoAppend(options.Workflows, pluginOptions.Workflows);
            options.Interceptors = ResolveAppend(options.Interceptors, pluginOptions.WorkerInterceptorsOption);
            options.WorkflowFailureExceptionTypes = ResolveAppend(options.WorkflowFailureExceptionTypes, pluginOptions.WorkflowFailureExceptionTypesOption);
        }

        public async Task<IEnumerable<WorkflowReplayResult>> RunReplayer(
            WorkflowReplayer replayer,
            Func<WorkflowReplayer, Task<IEnumerable<WorkflowReplayResult>>> continuation)
        {
            if (pluginOptions.RunContext != null)
            {
                var result = await pluginOptions.RunContext(async () => await continuation(replayer).ConfigureAwait(false)).ConfigureAwait(false);
                return result as IEnumerable<WorkflowReplayResult>;
            }
            return await continuation(replayer).ConfigureAwait(false);
        }

        private static T Resolve<T>(T existing, SimplePluginOptions.SimplePluginRequiredOption<T>? parameter)
        {
            if (parameter == null)
            {
                return existing;
            }
            var option = parameter.Constant ?? existing;
            if (parameter.Configurable != null)
            {
                return parameter.Configurable(option);
            }
            return option;
        }

        private static IReadOnlyCollection<T>? ResolveAppend<T>(
            IReadOnlyCollection<T>? existing,
            SimplePluginOptions.SimplePluginOption<IReadOnlyCollection<T>>? parameter)
        {
            if (parameter == null)
            {
                return existing;
            }

            var option = existing;
            if (existing != null && parameter.Constant != null)
            {
                option = existing.Concat(parameter.Constant).ToList();
            }
            else if (parameter.Constant != null)
            {
                option = parameter.Constant;
            }
            if (parameter.Configurable != null)
            {
                return parameter.Configurable(option);
            }
            return option;
        }

        private static void DoAppend<T>(
            IList<T> existing,
            IList<T>? parameter)
        {
            if (parameter == null)
            {
                return;
            }

            foreach (var item in parameter)
            {
                existing.Add(item);
            }
        }
    }
}