using System;
using System.Threading;

namespace Temporalio.Extensions.Hosting
{
    /// <summary>
    /// Provides an implementation of <see cref="IServiceProvider" /> based on
    /// the current execution context.
    /// </summary>
    public class ServiceProviderAccessor : IServiceProviderAccessor
    {
        private static readonly AsyncLocal<ServiceProviderHolder> ServiceProviderCurrent = new();

        /// <inheritdoc/>
        public IServiceProvider? ServiceProvider
        {
            get => ServiceProviderCurrent.Value?.ServiceProvider;

            set
            {
                var holder = ServiceProviderCurrent.Value;
                if (holder != null)
                {
                    // Clear current IServiceProvider trapped in the AsyncLocals, as its done.
                    holder.ServiceProvider = null;
                }

                if (value != null)
                {
                    // Use an object indirection to hold the IServiceProvider in the AsyncLocal,
                    // so it can be cleared in all ExecutionContexts when its cleared.
                    ServiceProviderCurrent.Value = new ServiceProviderHolder { ServiceProvider = value };
                }
            }
        }

        private sealed class ServiceProviderHolder
        {
            public IServiceProvider? ServiceProvider { get; set; }
        }
    }
}