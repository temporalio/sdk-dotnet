using System;
using System.Threading.Tasks;

namespace Temporalio.Converters
{
    /// <summary>
    /// Representation of a raw value that is still encoded and not converted.
    /// </summary>
    public interface IEncodedRawValue
    {
        /// <summary>
        /// Decode and convert the raw value to the given type.
        /// </summary>
        /// <typeparam name="T">Type to convert to.</typeparam>
        /// <returns>Decoded and converted value.</returns>
        Task<T> ToValueAsync<T>();

        /// <summary>
        /// Decode and convert the raw value to the given type.
        /// </summary>
        /// <param name="type">Type to convert to.</param>
        /// <returns>Decoded and converted value.</returns>
        Task<object?> ToValueAsync(Type type);
    }
}