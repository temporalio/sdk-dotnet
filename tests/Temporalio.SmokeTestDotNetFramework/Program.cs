using System;
using System.Threading.Tasks;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Testing;

namespace Temporalio.SmokeTestDotNetFramework
{
    internal class Program
    {
        public static async Task Main()
        {
            var env = await WorkflowEnvironment.StartLocalAsync();
            try
            {
                Console.WriteLine(
                    "System info: {0}",
                    await env.Client.WorkflowService.GetSystemInfoAsync(new GetSystemInfoRequest()));
            }
            finally
            {
                await env.ShutdownAsync();
            }
        }
    }
}
