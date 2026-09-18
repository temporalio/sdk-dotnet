using System;
using System.Collections.Generic;

namespace Temporalio.Converters
{
    /// <summary>
    /// Configuration for offloading large payloads to an external storage system.
    /// </summary>
    /// <remarks>
    /// This is validated when constructed rather than when used, so a misconfiguration surfaces at
    /// startup instead of on the first payload large enough to offload.
    /// </remarks>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    internal sealed class ExternalStorage
    {
        private const int DefaultPayloadSizeThreshold = 256 * 1024;

        private readonly Dictionary<string, IStorageDriver> driversByName;
        private readonly int payloadSizeThreshold = DefaultPayloadSizeThreshold;
        private readonly ExternalStorageConcurrency concurrency = ExternalStorageConcurrency.Default;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalStorage" /> class with a single
        /// driver that stores every eligible payload.
        /// </summary>
        /// <param name="driver">
        /// Driver used for storing and retrieving payloads. Must have a non-empty
        /// <see cref="IStorageDriver.Name" />.
        /// </param>
        /// <exception cref="ArgumentException">If the driver name is empty.</exception>
        /// <remarks>
        /// Retrieval routes by the name recorded in history, so a driver must remain configured for
        /// as long as any payload it stored can still be read.
        /// </remarks>
        public ExternalStorage(IStorageDriver driver)
            : this(new[] { driver }, (context, payload) => driver)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalStorage" /> class with a selector
        /// choosing among several drivers.
        /// </summary>
        /// <param name="drivers">
        /// Drivers available for storing and retrieving payloads. At least one is required, and
        /// every driver must have a unique, non-empty <see cref="IStorageDriver.Name" />.
        /// </param>
        /// <param name="driverSelector">
        /// Selector choosing which driver stores each payload.
        /// </param>
        /// <exception cref="ArgumentException">
        /// If no drivers are given, or if a driver name is empty or duplicated.
        /// </exception>
        /// <remarks>
        /// A selector is required here rather than optional, so that registering several drivers
        /// with no way to route between them cannot be expressed at all. Use
        /// <see cref="ExternalStorage(IStorageDriver)" /> for the single-driver case.
        /// </remarks>
        /// <remarks>
        /// Retrieval routes by the name recorded in history, so a driver must remain configured for
        /// as long as any payload it stored can still be read.
        /// </remarks>
        public ExternalStorage(
            IReadOnlyCollection<IStorageDriver> drivers,
            StorageDriverSelector driverSelector)
        {
            if (drivers == null)
            {
                throw new ArgumentNullException(nameof(drivers));
            }
            if (drivers.Count == 0)
            {
                throw new ArgumentException(
                    "At least one driver is required.", nameof(drivers));
            }
            if (driverSelector == null)
            {
                throw new ArgumentNullException(nameof(driverSelector));
            }
            driversByName = new(drivers.Count);
            // Copied so a caller mutating their collection afterwards cannot invalidate the checks
            // below or disagree with the name lookup.
            var copied = new IStorageDriver[drivers.Count];
            var index = 0;
            foreach (var driver in drivers)
            {
                if (driver == null)
                {
                    throw new ArgumentException("Driver cannot be null.", nameof(drivers));
                }
                // The name is the routing key written into history, so a driver without one can
                // never be resolved on the retrieval side.
                if (string.IsNullOrEmpty(driver.Name))
                {
                    throw new ArgumentException(
                        "Driver name cannot be null or empty.", nameof(drivers));
                }
                if (driversByName.ContainsKey(driver.Name))
                {
                    throw new ArgumentException(
                        $"Multiple drivers given with name '{driver.Name}'.", nameof(drivers));
                }
                driversByName[driver.Name] = driver;
                copied[index++] = driver;
            }
            Drivers = copied;
            DriverSelector = driverSelector;
        }

        /// <summary>
        /// Gets the drivers available for storing and retrieving payloads.
        /// </summary>
        public IReadOnlyCollection<IStorageDriver> Drivers { get; }

        /// <summary>
        /// Gets the selector choosing which driver stores each payload.
        /// </summary>
        /// <remarks>
        /// Only called for payloads that meet <see cref="PayloadSizeThreshold" />, and must return
        /// one of <see cref="Drivers" /> or null.
        /// </remarks>
        /// <remarks>
        /// Never null. Constructing with a single driver synthesizes a selector returning that
        /// driver, so a caller never has to distinguish the two cases.
        /// </remarks>
        public StorageDriverSelector DriverSelector { get; }

        /// <summary>
        /// Gets the minimum encoded payload size, in bytes, that is offloaded to external storage.
        /// Payloads at or above this size are offloaded; smaller ones are left inline. Defaults to
        /// 256KiB; set to <c>0</c> to offload every payload.
        /// </summary>
        /// <remarks>
        /// This is measured against the entire encoded payload, including metadata, and after any
        /// <see cref="IPayloadCodec" /> has run.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">If set to a negative value.</exception>
        public int PayloadSizeThreshold
        {
            get => payloadSizeThreshold;
            init
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value), value, "PayloadSizeThreshold cannot be negative.");
                }
                payloadSizeThreshold = value;
            }
        }

        /// <summary>
        /// Gets the limits on how many storage requests may be in flight at once. Defaults are
        /// suitable for most deployments.
        /// </summary>
        /// <exception cref="ArgumentNullException">If set to null.</exception>
        public ExternalStorageConcurrency Concurrency
        {
            get => concurrency;
            init => concurrency = value ??
                throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Get the driver registered under the given name.
        /// </summary>
        /// <param name="name">Driver name recorded in the payload reference.</param>
        /// <returns>The driver, or null if no driver is registered under that name.</returns>
        public IStorageDriver? GetDriver(string name) =>
            driversByName.TryGetValue(name, out var driver) ? driver : null;
    }
}
