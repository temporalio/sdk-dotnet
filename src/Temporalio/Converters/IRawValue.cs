using System;

namespace Temporalio.Converters
{
    /// <summary>
    /// Representation of a raw value that is already decoded but not converted.
    /// </summary>
    public interface IRawValue
    {
        /// <summary>
        /// Convert the raw value to the given type.
        /// </summary>
        /// <typeparam name="T">Type to convert to.</typeparam>
        /// <returns>Converted value.</returns>
        T ToValue<T>();

        /// <summary>
        /// Convert the raw value to the given type.
        /// </summary>
        /// <param name="type">Type to convert to.</param>
        /// <returns>Converted value.</returns>
        object? ToValue(Type type);
    }
}