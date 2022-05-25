using Temporal.TestUtil;
using Xunit.Abstractions;

namespace Temporal.Sdk.WorkflowClient.Test.E2EInt
{
    // ReSharper disable once UnusedType.Global
    public class SimpleClientInvocationsE2EWithTlsTest : SimpleClientInvocationsE2ETestBase
    {
        public SimpleClientInvocationsE2EWithTlsTest(ITestOutputHelper cout)
            : base(cout, TestTlsOptions.Server, 7234)
        {
        }
    }
}