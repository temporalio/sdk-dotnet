using System.Collections.Generic;
using Temporalio.Nexus;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for starting a standalone Nexus operation. <see cref="NexusOperationOptionsBase.Id" /> is required.
    /// </summary>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    public sealed class NexusOperationOptions : NexusOperationOptionsBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NexusOperationOptions"/> class.
        /// </summary>
        public NexusOperationOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NexusOperationOptions"/> class.
        /// </summary>
        /// <param name="id">Operation ID.</param>
        public NexusOperationOptions(string id)
        {
            Id = id;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NexusOperationOptions"/> class
        /// with the common settings from another options instance.
        /// </summary>
        /// <param name="options">The settings to copy.</param>
        internal NexusOperationOptions(NexusOperationOptionsBase options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets or sets the callbacks attached to this request.
        /// </summary>
        public IReadOnlyCollection<CompletionCallback>? CompletionCallbacks { get; set; }
    }
}
