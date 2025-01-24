namespace Temporalio.Workflows
{
    /// <summary>
    /// Actions taken if a workflow terminates with running handlers.
    /// </summary>
    /// <remarks>
    /// Policy defining actions taken when a workflow exits while update or signal handlers are
    /// running. The workflow exit may be due to successful return, failure, cancellation, or
    /// continue-as-new.
    /// </remarks>
    public enum HandlerUnfinishedPolicy
    {
        /// <summary>
        /// Issue a warning in addition to abandoning.
        /// </summary>
        WarnAndAbandon,

        /// <summary>
        /// Abandon the handler.
        /// </summary>
        /// <remarks>
        /// In the case of an update handler this means that the client will receive an error rather
        /// than the update result.
        /// </remarks>
        Abandon,
    }
}