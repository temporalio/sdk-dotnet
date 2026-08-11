using System.Diagnostics;
using Google.Protobuf;
using Temporalio.Api.Cloud.CloudService.V1;
using Temporalio.Api.Cloud.Namespace.V1;
using Temporalio.Api.Cloud.Operation.V1;
using Temporalio.Client;

namespace Temporalio.Tests;

internal static class CloudNamespaceCommand
{
    private const string CloudRegion = "aws-ca-central-1";
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(10);

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 1 && args[0] == "create")
        {
            await CreateAsync();
        }
        else if (args.Length == 2 && args[0] == "delete")
        {
            await DeleteAsync(args[1]);
        }
        else
        {
            throw new ArgumentException(
                "Usage: Temporalio.Tests cloud-namespace create | delete <namespace>",
                nameof(args));
        }
        return 0;
    }

    private static async Task CreateAsync()
    {
        var client = await ConnectAsync();
        var namespaceName = $"sdk-dotnet-ci-{RequiredEnv("GITHUB_RUN_ID")}-{RequiredEnv("GITHUB_RUN_ATTEMPT")}";
        var result = await client.CloudService.CreateNamespaceAsync(new()
        {
            AsyncOperationId = Guid.NewGuid().ToString(),
            Spec = new NamespaceSpec
            {
                Name = namespaceName,
                RetentionDays = 1,
                MtlsAuth = new()
                {
                    AcceptedClientCa = ByteString.CopyFrom(
                        await File.ReadAllBytesAsync(RequiredEnv("TEMPORAL_CLOUD_CLIENT_CA_PATH"))),
                    Enabled = true,
                },
                Replicas = { new ReplicaSpec { Region = CloudRegion } },
            },
        });
        if (string.IsNullOrEmpty(result.Namespace))
        {
            throw new InvalidOperationException("Create namespace response did not include a namespace");
        }

        // Persist the namespace before polling so cleanup can run if provisioning later fails.
        await File.AppendAllTextAsync(
            RequiredEnv("GITHUB_OUTPUT"),
            $"namespace={result.Namespace}{Environment.NewLine}");
        await WaitForOperationAsync(client, result.AsyncOperation);
    }

    private static async Task DeleteAsync(string namespaceName)
    {
        var client = await ConnectAsync();
        var existing = await client.CloudService.GetNamespaceAsync(new() { Namespace = namespaceName });
        var resourceVersion = existing.Namespace?.ResourceVersion;
        if (string.IsNullOrEmpty(resourceVersion))
        {
            throw new InvalidOperationException(
                $"Cloud namespace {namespaceName} did not include a resource version");
        }

        var result = await client.CloudService.DeleteNamespaceAsync(new()
        {
            Namespace = namespaceName,
            ResourceVersion = resourceVersion,
            AsyncOperationId = Guid.NewGuid().ToString(),
        });
        await WaitForOperationAsync(client, result.AsyncOperation);
    }

    private static Task<TemporalCloudOperationsClient> ConnectAsync() =>
        TemporalCloudOperationsClient.ConnectAsync(
            new(RequiredEnv("TEMPORAL_CLIENT_CLOUD_API_KEY"))
            {
                Version = RequiredEnv("TEMPORAL_CLIENT_CLOUD_API_VERSION"),
            });

    private static string RequiredEnv(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ?
            value :
            throw new InvalidOperationException($"Missing required environment variable {name}");

    private static async Task WaitForOperationAsync(
        TemporalCloudOperationsClient client,
        AsyncOperation? initialOperation)
    {
        var operationId = initialOperation?.Id;
        if (string.IsNullOrEmpty(operationId))
        {
            throw new InvalidOperationException("Cloud operation response did not include an ID");
        }

        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            var response = await client.CloudService.GetAsyncOperationAsync(
                new() { AsyncOperationId = operationId });
            var operation = response.AsyncOperation ??
                throw new InvalidOperationException($"Cloud operation {operationId} could not be read");
            if (operation.State == AsyncOperation.Types.State.Fulfilled)
            {
                return;
            }
            if (operation.State is AsyncOperation.Types.State.Failed or
                AsyncOperation.Types.State.Cancelled or
                AsyncOperation.Types.State.Rejected)
            {
                throw new InvalidOperationException(
                    $"Cloud operation {operationId} {operation.State}: {operation.FailureReason}");
            }

            var remaining = OperationTimeout - elapsed.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException($"Timed out waiting for Cloud operation {operationId}");
            }

            // Honor the server hint so the control plane can throttle polling when needed.
            var delay = operation.CheckDuration?.ToTimeSpan() ?? TimeSpan.Zero;
            if (delay < TimeSpan.FromSeconds(1))
            {
                delay = TimeSpan.FromSeconds(1);
            }
            await Task.Delay(delay < remaining ? delay : remaining);
        }
    }
}
