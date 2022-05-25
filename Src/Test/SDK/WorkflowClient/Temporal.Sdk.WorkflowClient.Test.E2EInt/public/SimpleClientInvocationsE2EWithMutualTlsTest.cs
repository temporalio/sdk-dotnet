using Temporal.TestUtil;
using Xunit.Abstractions;

namespace Temporal.Sdk.WorkflowClient.Test.E2EInt
{
    // ReSharper disable once UnusedType.Global
    public class SimpleClientInvocationsE2EWithMutualTlsTest : SimpleClientInvocationsE2ETestBase
    {
        public SimpleClientInvocationsE2EWithMutualTlsTest(ITestOutputHelper cout)
            : base(cout, TestTlsOptions.Mutual, 7235)
        {
        }
    }
}