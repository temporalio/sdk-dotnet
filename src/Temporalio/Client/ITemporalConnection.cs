namespace Temporalio.Client
{
    /// <summary>
    /// Interface to a connection to Temporal.
    /// </summary>
    /// <seealso cref="TemporalConnection" />
    public interface ITemporalConnection : IBridgeClientProvider
    {
        /// <summary>
        /// Gets the raw workflow service.
        /// </summary>
        public WorkflowService WorkflowService { get; }

        /// <summary>
        /// Gets the raw operator service.
        /// </summary>
        public OperatorService OperatorService { get; }

        /// <summary>
        /// Gets the raw gRPC test service.
        /// </summary>
        /// <remarks>
        /// Only the <see cref="Temporalio.Testing.WorkflowEnvironment.StartTimeSkippingAsync" />
        /// environment has this service implemented.
        /// </remarks>
        public TestService TestService { get; }
    }
}
