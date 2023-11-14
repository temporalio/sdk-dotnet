using System;
using Microsoft.Extensions.Logging;

namespace Temporalio.Runtime
{
    /// <summary>
    /// Log forwarding options to send Core logs to a logger.
    /// </summary>
    public class LogForwardingOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LogForwardingOptions"/> class.
        /// </summary>
        public LogForwardingOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LogForwardingOptions"/> class.
        /// </summary>
        /// <param name="logger">Logger to send to.</param>
        public LogForwardingOptions(ILogger logger) => Logger = logger;

        /// <summary>
        /// Gets or sets the logger that logs will be forwarded to. This is required.
        /// </summary>
        public ILogger? Logger { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether additional fields are parsed and appended to the
        /// log message. Default is true.
        /// </summary>
        public bool IncludeFields { get; set; } = true;

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone() => (LogForwardingOptions)MemberwiseClone();
    }
}
