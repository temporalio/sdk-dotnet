namespace Temporalio.Tests.Extensions.Aws.Lambda;

using Xunit;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TemporalLambdaWorkerNonParallelDefinition
{
    public const string Name = "TemporalLambdaWorkerNonParallel";
}
