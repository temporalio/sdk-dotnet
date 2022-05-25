using Temporal.TestUtil;
using Xunit.Abstractions;

namespace Temporal.Sdk.WorkflowClient.Test.Int
{
    // ReSharper disable once UnusedType.Global
    public class TemporalClientTlsTest : TemporalClientTestBase
    {
        public TemporalClientTlsTest(ITestOutputHelper cout)
            : base(cout, TestTlsOptions.Server, 7235)
        {
        }
    }
}