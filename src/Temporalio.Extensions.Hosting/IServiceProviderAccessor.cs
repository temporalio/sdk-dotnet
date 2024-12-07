using System;

namespace Temporalio.Extensions.Hosting
{
    /// <summary>
    /// Provides access to the current, scoped <see cref="IServiceProvider"/> if
    /// one is available.
    /// </summary>
    public interface IServiceProviderAccessor
    {
        /// <summary>
        /// Gets or sets the current service provider.
        /// </summary>
        IServiceProvider? ServiceProvider { get; set; }
    }
}