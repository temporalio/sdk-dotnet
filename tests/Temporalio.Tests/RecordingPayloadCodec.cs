using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Temporalio.Tests
{
    /// <summary>
    /// Records every serialization context it is handed, so a test can assert which contexts the
    /// SDK scoped a conversion by. Derive from this to add behavior on top of the recording.
    /// </summary>
    public class RecordingPayloadCodec : IPayloadCodec, IWithSerializationContext<IPayloadCodec>
    {
        private readonly List<ISerializationContext?> seen;

        public RecordingPayloadCodec()
            : this(new List<ISerializationContext?>(), null)
        {
        }

        protected RecordingPayloadCodec(
            List<ISerializationContext?> seen, ISerializationContext? context)
        {
            this.seen = seen;
            Context = context;
        }

        /// <summary>
        /// Gets every context recorded so far. Shared by each instance derived through
        /// <see cref="WithSerializationContext"/>, so a test sees them all.
        /// </summary>
        public IReadOnlyList<ISerializationContext?> Contexts
        {
            get
            {
                lock (seen)
                {
                    return seen.ToList();
                }
            }
        }

        /// <summary>
        /// Gets the recorded contexts that are Nexus contexts.
        /// </summary>
        public IReadOnlyList<ISerializationContext.Nexus> NexusContexts =>
            Contexts.OfType<ISerializationContext.Nexus>().ToList();

        /// <summary>
        /// Gets the context this instance was scoped to, or null if it was not scoped.
        /// </summary>
        protected ISerializationContext? Context { get; }

        /// <summary>
        /// Gets the recording list, shared with every instance derived from this one, so a
        /// subclass can pass it to its own derived instances.
        /// </summary>
        protected List<ISerializationContext?> Seen => seen;

        public void Reset()
        {
            lock (seen)
            {
                seen.Clear();
            }
        }

        public virtual IPayloadCodec WithSerializationContext(ISerializationContext context) =>
            new RecordingPayloadCodec(seen, context);

        public virtual async Task<IReadOnlyCollection<Payload>> EncodeAsync(
            IReadOnlyCollection<Payload> payloads)
        {
            // Yield first so the recorded context is the one that survived the continuation.
            await Task.Yield();
            Record();
            return payloads;
        }

        public virtual async Task<IReadOnlyCollection<Payload>> DecodeAsync(
            IReadOnlyCollection<Payload> payloads)
        {
            await Task.Yield();
            Record();
            return payloads;
        }

        /// <summary>
        /// Record this instance's context as one the SDK scoped a conversion by.
        /// </summary>
        protected void Record()
        {
            lock (seen)
            {
                seen.Add(Context);
            }
        }
    }
}
