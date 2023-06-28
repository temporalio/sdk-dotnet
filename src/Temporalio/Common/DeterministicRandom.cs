#pragma warning disable CA1001 // We know that we have an underlying random handle we instead dispose on destruct
using System;

namespace Temporalio.Common
{
    /// <summary>
    /// Implementation of <see cref="Random" /> that is deterministic and supports a 64-bit seed,
    /// unlike the standard implementation which is only 32-bit. Internally this uses a
    /// well-known/tested PCG-based algorithm (specifically, 128-bit MCG PCG-XSL-RR aka pcg64_fast),
    /// see https://www.pcg-random.org/. Changing of this internal algorithm in any way is
    /// considered a backwards incompatible alteration.
    /// </summary>
    public class DeterministicRandom : Random
    {
        private readonly Bridge.DeterministicRandom underlying;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeterministicRandom" /> class.
        /// </summary>
        /// <param name="seed">Seed for this random.</param>
        public DeterministicRandom(ulong seed) => underlying = new(seed);

        /// <summary>
        /// Finalizes an instance of the <see cref="DeterministicRandom"/> class.
        /// </summary>
        ~DeterministicRandom() => underlying.Dispose();

        /// <inheritdoc />
        public override int Next() => Next(int.MaxValue);

        /// <inheritdoc />
        public override int Next(int maxValue) => Next(0, maxValue);

        /// <inheritdoc />
        public override int Next(int minValue, int maxValue) =>
            underlying.RandomInt32(minValue, maxValue, false);

        /// <inheritdoc />
        public override double NextDouble() => Sample();

        /// <inheritdoc />
        public override void NextBytes(byte[] buffer) => underlying.RandomFillBytes(buffer);

#if NETSTANDARD2_1_OR_GREATER
        /// <summary>
        /// Intentionally not implemented. Currently filling byte spans spans is unsupported.
        /// </summary>
        /// <param name="buffer">Buffer.</param>
        public override void NextBytes(Span<byte> buffer) =>
            // Internally this defaults to looping over the buffer and filling with (byte)Next()
            // which is not the same as NextBytes(buffer) above. Rather than implement ByteArrayRef
            // for spans in the bridge at this time, we will just forbid the use of spans here until
            // we do implement it.
            throw new NotImplementedException("Spans not implemented at this time");
#endif

        /// <inheritdoc />
        protected override double Sample() => underlying.RandomDouble(0, 1, false);
    }
}