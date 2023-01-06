namespace Temporalio.Tests;

using Xunit;

[CollectionDefinition("Environment")]
public class WorkflowEnvironmentCollection : ICollectionFixture<WorkflowEnvironment> { }
