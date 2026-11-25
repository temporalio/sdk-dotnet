namespace Temporalio.Tests.Extensions.Aws.SecretsManager;

using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

/// <summary>
/// A Secrets Manager client that answers GetSecretValue with a function, without network calls.
/// </summary>
internal sealed class FakeSecretsManagerClient : AmazonSecretsManagerClient
{
    private readonly Func<GetSecretValueRequest, GetSecretValueResponse> getSecretValue;

    public FakeSecretsManagerClient(Func<GetSecretValueRequest, GetSecretValueResponse> getSecretValue)
        : base(new AnonymousAWSCredentials(), Amazon.RegionEndpoint.USEast1) =>
        this.getSecretValue = getSecretValue;

    public override Task<GetSecretValueResponse> GetSecretValueAsync(
        GetSecretValueRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(getSecretValue(request));
}
