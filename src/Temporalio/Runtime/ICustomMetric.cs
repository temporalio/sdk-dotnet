#pragma warning disable CA1040 // We are ok with an empty interface here

namespace Temporalio.Runtime
{
    /// <summary>
    /// Base interface for all custom metric implementations.
    /// </summary>
    /// <typeparam name="T">Type of value for the metric.</typeparam>
    public interface ICustomMetric<T>
        where T : struct
    {
    }
}