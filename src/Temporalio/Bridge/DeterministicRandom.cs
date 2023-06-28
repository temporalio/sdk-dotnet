using System;
using System.Runtime.InteropServices;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core-owned random number generator. Not thread safe.
    /// </summary>
    internal class DeterministicRandom : SafeHandle
    {
        private readonly unsafe Interop.Random* ptr;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeterministicRandom" /> class.
        /// </summary>
        /// <param name="seed">Seed to initialize with.</param>
        public DeterministicRandom(ulong seed)
            : base(IntPtr.Zero, true)
        {
            unsafe
            {
                ptr = Interop.Methods.random_new(seed);
                SetHandle((IntPtr)ptr);
            }
        }

        /// <inheritdoc />
        public override unsafe bool IsInvalid => false;

        /// <summary>
        /// Generate a random integer within the given range.
        /// </summary>
        /// <param name="min">Inclusive minimum.</param>
        /// <param name="max">Maximum.</param>
        /// <param name="maxInclusive">Whether max is inclusive or not.</param>
        /// <returns>Random integer.</returns>
        public int RandomInt32(int min, int max, bool maxInclusive)
        {
            unsafe
            {
                return Interop.Methods.random_int32_range(
                    ptr, min, max, (byte)(maxInclusive ? 1 : 0));
            }
        }

        /// <summary>
        /// Generate a random double within the given range.
        /// </summary>
        /// <param name="min">Inclusive minimum.</param>
        /// <param name="max">Maximum.</param>
        /// <param name="maxInclusive">Whether max is inclusive or not.</param>
        /// <returns>Random double.</returns>
        public double RandomDouble(double min, double max, bool maxInclusive)
        {
            unsafe
            {
                return Interop.Methods.random_double_range(
                    ptr, min, max, (byte)(maxInclusive ? 1 : 0));
            }
        }

        /// <summary>
        /// Fill the given array with random bytes.
        /// </summary>
        /// <param name="bytes">Byte array to fill.</param>
        public void RandomFillBytes(byte[] bytes)
        {
            using (var scope = new Scope())
            {
                unsafe
                {
                    Interop.Methods.random_fill_bytes(ptr, scope.ByteArray(bytes));
                }
            }
        }

        /// <inheritdoc />
        protected override unsafe bool ReleaseHandle()
        {
            Interop.Methods.random_free(ptr);
            return true;
        }
    }
}