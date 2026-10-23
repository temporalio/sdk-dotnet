using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Api.Common.V1;
using Temporalio.Exceptions;

namespace Temporalio.Converters
{
    /// <summary>
    /// Stores large payloads in a storage driver and fetches them back. These take the payloads of
    /// one field; walking a whole message and applying them to each field is the caller's.
    /// </summary>
    /// <remarks>
    /// References are stored and read in plain form, so a reference's claim data is written to
    /// history unencrypted. The payload codec is not applied to references, and the configured
    /// concurrency limits are not enforced.
    /// </remarks>
    internal sealed class ExternalStorageRunner
    {
        private readonly ExternalStorage storage;
        private readonly IStorageDriverLimiter<Payload> storeLimiter;
        private readonly IStorageDriverLimiter<StorageDriverClaim> retrieveLimiter;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalStorageRunner"/> class.
        /// </summary>
        /// <param name="storage">Configured drivers, selector, and size threshold.</param>
        /// <param name="storeLimiter">Limiter the store contexts carry for drivers to use.</param>
        /// <param name="retrieveLimiter">
        /// Limiter the retrieve contexts carry for drivers to use.
        /// </param>
        public ExternalStorageRunner(
            ExternalStorage storage,
            IStorageDriverLimiter<Payload> storeLimiter,
            IStorageDriverLimiter<StorageDriverClaim> retrieveLimiter)
        {
            this.storage = storage;
            this.storeLimiter = storeLimiter;
            this.retrieveLimiter = retrieveLimiter;
        }

        /// <summary>
        /// Replace every payload at or above the configured size threshold with a reference,
        /// preserving order. Payloads below the threshold, or that the selector declines, are left
        /// as they are.
        /// </summary>
        /// <param name="payloads">Payloads of one field.</param>
        /// <param name="target">The execution the payloads are stored for, when there is one.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The payloads with each stored one replaced by a reference.</returns>
        public async Task<IReadOnlyCollection<Payload>> StoreAsync(
            IReadOnlyCollection<Payload> payloads,
            IStorageDriverTargetInfo? target,
            CancellationToken cancellationToken)
        {
            if (payloads.Count == 0)
            {
                return payloads;
            }

            var result = payloads.ToArray();
            var selectContext = new StorageDriverSelectContext(target);
            var groups = new Dictionary<
                string, (IStorageDriver Driver, List<(int Index, long Size)> Items)>();
            for (var i = 0; i < result.Length; i++)
            {
                var payload = result[i];
                long size = payload.CalculateSize();
                if (size < storage.PayloadSizeThreshold)
                {
                    continue;
                }
                var driver = storage.DriverSelector(selectContext, payload);
                if (driver is null)
                {
                    continue;
                }
                // Checked by reference, so a driver built to look like a registered one is rejected.
                if (!ReferenceEquals(storage.GetDriver(driver.Name), driver))
                {
                    throw new InvalidOperationException(
                        "Storage driver selector returned a driver that is not registered: " +
                        $"'{driver.Name}'.");
                }
                if (!groups.TryGetValue(driver.Name, out var group))
                {
                    group = (driver, new List<(int, long)>());
                    groups[driver.Name] = group;
                }
                group.Items.Add((i, size));
            }

            if (groups.Count == 0)
            {
                return payloads;
            }

            var context = new StorageDriverStoreContext(target, storeLimiter);
            await Task.WhenAll(groups.Values.Select(async group =>
            {
                var toStore = group.Items.Select(item => result[item.Index]).ToArray();
                var claims = await group.Driver
                    .StoreAsync(context, toStore, cancellationToken).ConfigureAwait(false);
                if (claims.Count != group.Items.Count)
                {
                    throw new InvalidOperationException(
                        $"Storage driver '{group.Driver.Name}' returned {claims.Count} claims " +
                        $"for {group.Items.Count} payloads.");
                }
                foreach (var (item, claim) in group.Items.Zip(claims, (i, c) => (i, c)))
                {
                    result[item.Index] = ExternalStorageReferences.CreateReferencePayload(
                        group.Driver.Name, claim.ClaimData, item.Size);
                }
            })).ConfigureAwait(false);
            return result;
        }

        /// <summary>
        /// Store a single payload, replacing its contents in place if it is stored.
        /// </summary>
        /// <param name="payload">Payload to store, modified in place.</param>
        /// <param name="target">The execution the payload is stored for, when there is one.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that completes when the payload is stored.</returns>
        public async Task StoreAsync(
            Payload payload, IStorageDriverTargetInfo? target, CancellationToken cancellationToken)
        {
            // The driver may keep the payload it is handed, and this one is overwritten below, so it
            // gets a copy. Cloning shares the immutable data bytes, so only the metadata is copied.
            var copy = payload.Clone();
            var result = (await StoreAsync(new[] { copy }, target, cancellationToken)
                .ConfigureAwait(false)).Single();
            if (!ReferenceEquals(result, copy))
            {
                payload.ReplaceWith(result);
            }
        }

        /// <summary>
        /// Replace every reference with the payload the driver returns for it, preserving order.
        /// </summary>
        /// <param name="payloads">Payloads of one field.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The payloads with each reference replaced by its stored payload.</returns>
        public async Task<IReadOnlyCollection<Payload>> RetrieveAsync(
            IReadOnlyCollection<Payload> payloads, CancellationToken cancellationToken)
        {
            if (payloads.Count == 0)
            {
                return payloads;
            }

            var result = payloads.ToArray();
            var groups = new Dictionary<string, List<(int Index, StorageDriverClaim Claim)>>();
            for (var i = 0; i < result.Length; i++)
            {
                if (!ExternalStorageReferences.IsReference(result[i]))
                {
                    continue;
                }
                var reference = ExternalStorageReferences.ParseReference(result[i]);
                if (!groups.TryGetValue(reference.DriverName, out var items))
                {
                    items = new List<(int, StorageDriverClaim)>();
                    groups[reference.DriverName] = items;
                }
                items.Add((i, new StorageDriverClaim(reference.ClaimData.ToDictionary(
                    pair => pair.Key, pair => pair.Value))));
            }

            if (groups.Count == 0)
            {
                return payloads;
            }

            // Resolve every driver first, so an unregistered name fails before any request is made.
            var work = new List<(IStorageDriver Driver, List<(int Index, StorageDriverClaim Claim)> Items)>();
            foreach (var group in groups)
            {
                var driver = storage.GetDriver(group.Key) ??
                    throw new InvalidExternalStorageReferenceException(
                        "External storage reference names a driver that is not registered: " +
                        $"'{group.Key}'.",
                        inner: null);
                work.Add((driver, group.Value));
            }

            var context = new StorageDriverRetrieveContext(retrieveLimiter);
            await Task.WhenAll(work.Select(async entry =>
            {
                var claims = entry.Items.Select(item => item.Claim).ToArray();
                var retrieved = await entry.Driver
                    .RetrieveAsync(context, claims, cancellationToken).ConfigureAwait(false);
                if (retrieved.Count != entry.Items.Count)
                {
                    throw new InvalidOperationException(
                        $"Storage driver '{entry.Driver.Name}' returned {retrieved.Count} " +
                        $"payloads for {entry.Items.Count} claims.");
                }
                foreach (var (item, payload) in entry.Items.Zip(retrieved, (i, p) => (i, p)))
                {
                    result[item.Index] = payload;
                }
            })).ConfigureAwait(false);
            return result;
        }

        /// <summary>
        /// Retrieve a single reference, replacing its contents in place.
        /// </summary>
        /// <param name="payload">Payload to retrieve if it is a reference, modified in place.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that completes when the payload is retrieved.</returns>
        public async Task RetrieveAsync(Payload payload, CancellationToken cancellationToken)
        {
            var result = (await RetrieveAsync(new[] { payload }, cancellationToken)
                .ConfigureAwait(false)).Single();
            if (!ReferenceEquals(result, payload))
            {
                payload.ReplaceWith(result);
            }
        }
    }
}
