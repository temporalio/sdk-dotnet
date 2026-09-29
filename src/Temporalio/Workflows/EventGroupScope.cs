using System;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Disposable scope returned by <see cref="Workflow.WithEventGroups" />. Commands produced
    /// while this is undisposed carry the Event Groups passed to that call, composed with any
    /// enclosing scopes.
    /// </summary>
    /// <remarks>
    /// Dispose nested scopes in reverse order of creation. Disposing a scope that is not the
    /// active one throws <see cref="InvalidOperationException" /> and leaves the active scope
    /// in place.
    /// </remarks>
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

        /// <summary>
        /// Restores the Event Groups that were active when this scope was created.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// This scope is not the active scope. Nested scopes must be disposed first. The active
        /// scope is left unchanged, and this scope can be disposed after them.
        /// </exception>
        public void Dispose()
        {
            if (!disposed)
            {
                restore();
                disposed = true;
            }
        }
    }
}
