namespace Temporalio.Runtime
{
    /// <summary>
    /// Format for Core logs written to the console.
    /// </summary>
    public enum ConsoleLogFormat
    {
        /// <summary>
        /// Compact single-line text output.
        /// </summary>
        Compact,

        /// <summary>
        /// Human-readable multi-line output.
        /// </summary>
        Pretty,

        /// <summary>
        /// Newline-delimited JSON output.
        /// </summary>
        Json,
    }
}
