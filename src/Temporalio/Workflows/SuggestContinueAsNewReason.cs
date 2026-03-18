namespace Temporalio.Workflows
{
    /// <summary>
    /// Specifies why continue-as-new is suggested.
    /// </summary>
    /// <remarks>WARNING: May be removed or changed in the future.</remarks>
    public enum SuggestContinueAsNewReason
    {
        /// <summary>
        /// Unspecified reason.
        /// </summary>
        Unspecified = Temporalio.Api.Enums.V1.SuggestContinueAsNewReason.Unspecified,

        /// <summary>
        /// Workflow history size is getting too large.
        /// </summary>
        HistorySizeTooLarge = Temporalio.Api.Enums.V1.SuggestContinueAsNewReason.HistorySizeTooLarge,

        /// <summary>
        /// Workflow history event count is getting too large.
        /// </summary>
        TooManyHistoryEvents = Temporalio.Api.Enums.V1.SuggestContinueAsNewReason.TooManyHistoryEvents,

        /// <summary>
        /// Workflow's count of completed plus in-flight updates is too large.
        /// </summary>
        TooManyUpdates = Temporalio.Api.Enums.V1.SuggestContinueAsNewReason.TooManyUpdates,
    }
}
