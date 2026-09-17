using System;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Disposable scope returned by <see cref="Workflow.WithEventGroups" />. Commands produced
    /// while this is undisposed carry the Event Groups passed to that call, composed with any
    /// enclosing scopes.
    /// </summary>
    /// <remarks>WARNING: Event Groups are experimental.</remarks>
    public sealed class EventGroupScope : IDisposable
    {
        private readonly Action restore;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventGroupScope"/> class.
        /// </summary>
        /// <param name="restore">Action that restores the previous ambient Event Groups.</param>
        internal EventGroupScope(Action restore) => this.restore = restore;

        /// <inheritdoc />
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                restore();
            }
        }
    }
}
