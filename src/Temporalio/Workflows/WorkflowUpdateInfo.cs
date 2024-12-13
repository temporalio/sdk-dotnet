using System.Collections.Generic;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Information about the current update. This set via a
    /// <see cref="System.Threading.AsyncLocal{T}" /> and therefore only visible inside the handler
    /// and tasks it creates.
    /// </summary>
    /// <param name="Id">Current update ID.</param>
    /// <param name="Name">Current update name.</param>
    public record WorkflowUpdateInfo(
        string Id,
        string Name)
    {
        /// <summary>
        /// Creates the value that is set on
        /// <see cref="Microsoft.Extensions.Logging.ILogger.BeginScope" /> for the update handler.
        /// </summary>
        /// <returns>Scope.</returns>
        internal Dictionary<string, object> CreateLoggerScope() => new()
        {
            ["UpdateId"] = Id,
            ["UpdateName"] = Name,
        };
    }
}