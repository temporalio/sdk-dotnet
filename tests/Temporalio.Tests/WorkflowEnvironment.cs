namespace Temporalio.Tests;

using System;
using Xunit;

public class WorkflowEnvironment : IAsyncLifetime
{
    private Testing.WorkflowEnvironment? env;

    public Temporalio.Client.ITemporalClient Client =>
        env?.Client ?? throw new InvalidOperationException("Environment not created");

    public async Task InitializeAsync()
    {
        // TODO(cretz): Support other environments
        env = await Testing.WorkflowEnvironment.StartLocalAsync();
    }

    public async Task DisposeAsync()
    {
        if (env != null)
        {
            await env.ShutdownAsync();
        }
    }
}
