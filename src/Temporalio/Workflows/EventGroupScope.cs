using System;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Disposable scope returned by <see cref="Workflow.WithEventGroups" />. Commands produced
    /// while this is undisposed carry the Event Groups passed to that call, composed with any
    /// enclosing scopes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Dispose nested scopes in reverse order of creation. Disposing a scope that is not the
    /// active one throws <see cref="InvalidOperationException" /> and leaves the active scope
    /// in place.
    /// </para>
    /// <para>WARNING: Event Groups are experimental.</para>
    /// </remarks>
    public sealed class EventGroupScope : IDisposable
    {
        private readonly EventGroupAmbient.State installed;
        private readonly EventGroupAmbient.State previous;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventGroupScope"/> class.
        /// </summary>
        /// <param name="installed">State this scope installed.</param>
        /// <param name="previous">State to restore on dispose.</param>
        internal EventGroupScope(EventGroupAmbient.State installed, EventGroupAmbient.State previous)
        {
            this.installed = installed;
            this.previous = previous;
        }

        /// <summary>
        /// Restores the Event Groups that were active when this scope was created.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// This scope is not the active scope. Nested scopes must be disposed first.
        /// The active scope is left unchanged, and this scope can be disposed after them.
        /// </exception>
        public void Dispose()
        {
            if (!disposed)
            {
                EventGroupAmbient.Restore(installed, previous);
                disposed = true;
            }
        }
    }
}
