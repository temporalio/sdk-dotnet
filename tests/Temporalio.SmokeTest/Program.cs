using Temporalio.Testing;

await using var env = await WorkflowEnvironment.StartLocalAsync();

Console.WriteLine(
    "System info: {0}",
    await env.Client.Connection.WorkflowService.GetSystemInfoAsync(new()));