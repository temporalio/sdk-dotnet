using Temporal.TestUtil;
using Xunit.Abstractions;

namespace Temporal.Sdk.WorkflowClient.Test.Int
{
    // ReSharper disable once UnusedType.Global
    public class TemporalClientMutualTlsTest : TemporalClientTestBase
    {
        public TemporalClientMutualTlsTest(ITestOutputHelper cout)
            : base(cout, TestTlsOptions.Mutual, 7234)
        {
        }
    }
}