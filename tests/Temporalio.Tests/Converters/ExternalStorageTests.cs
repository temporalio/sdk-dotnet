namespace Temporalio.Tests.Converters;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using Xunit;
using Xunit.Abstractions;

public class ExternalStorageTests : TestBase
{
    public ExternalStorageTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public void NewExternalStorage_Defaults_AreApplied()
    {
        var driver = new StubStorageDriver();
        var storage = new ExternalStorage(driver);

        Assert.Equal(256 * 1024, storage.PayloadSizeThreshold);
        Assert.Equal(64, storage.Concurrency.MaxDriverOperations);
        Assert.Equal(8, storage.Concurrency.MaxOperationsPerMessage);
        Assert.Same(driver, Assert.Single(storage.Drivers));
        Assert.Same(driver, storage.DriverSelector(new StorageDriverSelectContext(null), new Payload()));
    }

    [Fact]
    public void NewExternalStorage_InitProperties_AreApplied()
    {
        var storage = new ExternalStorage(new StubStorageDriver())
        {
            PayloadSizeThreshold = 0,
            Concurrency = ExternalStorageConcurrency.Default with
            {
                MaxDriverOperations = 5,
                MaxOperationsPerMessage = 2,
            },
        };

        // Zero means offload everything rather than disabling offload.
        Assert.Equal(0, storage.PayloadSizeThreshold);
        Assert.Equal(5, storage.Concurrency.MaxDriverOperations);
        Assert.Equal(2, storage.Concurrency.MaxOperationsPerMessage);
    }

    [Fact]
    public void Concurrency_DefaultWith_CopiesUnsetLimitsAndStillValidates()
    {
        var custom = ExternalStorageConcurrency.Default with { MaxOperationsPerMessage = 5 };

        Assert.Equal(5, custom.MaxOperationsPerMessage);
        Assert.Equal(64, custom.MaxDriverOperations);
        Assert.Equal(8, ExternalStorageConcurrency.Default.MaxOperationsPerMessage);

        // A "with" expression assigns through the init accessor, so the range check still runs.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExternalStorageConcurrency.Default with { MaxOperationsPerMessage = 0 });
    }

    [Fact]
    public void NewConcurrency_MaxDriverOperationsBelowOne_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ExternalStorageConcurrency { MaxDriverOperations = 0 });

    [Fact]
    public void NewConcurrency_MaxOperationsPerMessageBelowOne_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ExternalStorageConcurrency { MaxOperationsPerMessage = 0 });

    [Fact]
    public void NewExternalStorage_NullConcurrency_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new ExternalStorage(new StubStorageDriver()) { Concurrency = null! });

    [Fact]
    public void NewExternalStorage_NoDrivers_Throws()
    {
        var err = Assert.Throws<ArgumentException>(
            () => new ExternalStorage(
                Array.Empty<IStorageDriver>(), (context, payload) => null));
        Assert.Contains("At least one driver", err.Message);
    }

    [Fact]
    public void NewExternalStorage_EmptyDriverName_Throws()
    {
        // The name is the routing key in history, so an unnamed driver is unresolvable on read.
        var err = Assert.Throws<ArgumentException>(
            () => new ExternalStorage(new StubStorageDriver(name: string.Empty)));
        Assert.Contains("cannot be null or empty", err.Message);
    }

    [Fact]
    public void NewExternalStorage_DuplicateDriverNames_Throws()
    {
        var err = Assert.Throws<ArgumentException>(() => new ExternalStorage(
            new[] { new StubStorageDriver(name: "dup"), new StubStorageDriver(name: "dup") },
            (context, payload) => null));
        Assert.Contains("Multiple drivers given with name 'dup'", err.Message);
    }

    [Fact]
    public void NewExternalStorage_MultipleDriversWithSelector_IsAllowed()
    {
        var first = new StubStorageDriver(name: "a");
        var storage = new ExternalStorage(
            new[] { first, new StubStorageDriver(name: "b") },
            (context, payload) => first);

        Assert.Equal(2, storage.Drivers.Count);
        Assert.Same(
            first, storage.DriverSelector(new StorageDriverSelectContext(null), new Payload()));
    }

    [Fact]
    public void NewExternalStorage_NullSelector_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new ExternalStorage(
            new[] { new StubStorageDriver(name: "a"), new StubStorageDriver(name: "b") }, null!));

    [Fact]
    public void NewExternalStorage_NegativeThreshold_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ExternalStorage(new StubStorageDriver())
            {
                PayloadSizeThreshold = -1,
            });

    [Fact]
    public void NewExternalStorage_MutatedDriverList_DoesNotAffectStorage()
    {
        var driver = new StubStorageDriver();
        var drivers = new List<IStorageDriver> { driver };
        var storage = new ExternalStorage(drivers, (context, payload) => driver);

        drivers.Add(new StubStorageDriver(name: "added"));

        Assert.Single(storage.Drivers);
        Assert.Null(storage.GetDriver("added"));
    }

    [Fact]
    public void GetDriver_ByName_ResolvesRegisteredDriver()
    {
        var driver = new StubStorageDriver(name: "known");
        var storage = new ExternalStorage(driver);

        Assert.Same(driver, storage.GetDriver("known"));
        Assert.Null(storage.GetDriver("unknown"));
    }

    [Fact]
    public void StoreContext_SameValues_AreEqual()
    {
        var target = new IStorageDriverTargetInfo.Workflow("ns", "wf-id", "run-id", "wf-type");
        var limiter = new PassThroughLimiter<Payload>();
        var context = new StorageDriverStoreContext(target, limiter);

        Assert.Equal(
            new StorageDriverStoreContext(
                new IStorageDriverTargetInfo.Workflow("ns", "wf-id", "run-id", "wf-type"), limiter),
            context);
        Assert.NotEqual(
            new StorageDriverStoreContext(null, limiter), context);
    }

    [Fact]
    public void StoreContext_NoTarget_IsAllowed()
    {
        // Nexus operation payloads are stored without an associated execution.
        var context = new StorageDriverStoreContext(null, new PassThroughLimiter<Payload>());

        Assert.Null(context.Target);
        Assert.NotNull(context.Limiter);
    }

    [Fact]
    public void Target_ActivityAndWorkflow_AreDistinctTypes()
    {
        IStorageDriverTargetInfo workflow =
            new IStorageDriverTargetInfo.Workflow("ns", "wf-id", "run-id", "wf-type");
        IStorageDriverTargetInfo activity =
            new IStorageDriverTargetInfo.Activity("ns", "act-id", "run-id", "act-type");

        Assert.IsType<IStorageDriverTargetInfo.Workflow>(workflow);
        Assert.IsType<IStorageDriverTargetInfo.Activity>(activity);
        Assert.NotEqual<object>(workflow, activity);
    }

    [Fact]
    public void Claim_SameClaimData_IsEqual()
    {
        var claimData = new Dictionary<string, string> { ["key"] = "k" };

        Assert.Equal(new StorageDriverClaim(claimData), new StorageDriverClaim(claimData));
    }

    private class PassThroughLimiter<TItem> : IStorageDriverLimiter<TItem>
    {
        public Task<TResult> RunAsync<TResult>(
            TItem item, Func<Task<TResult>> operation, CancellationToken cancellationToken) =>
            operation();
    }

    private class StubStorageDriver : IStorageDriver
    {
        private readonly string name;

        public StubStorageDriver(string name = "stub") => this.name = name;

        public string Name => name;

        public string Type => "test.stubdriver";

        public Task<IReadOnlyCollection<StorageDriverClaim>> StoreAsync(
            StorageDriverStoreContext context,
            IReadOnlyCollection<Payload> payloads,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<IReadOnlyCollection<Payload>> RetrieveAsync(
            StorageDriverRetrieveContext context,
            IReadOnlyCollection<StorageDriverClaim> claims,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }
}
