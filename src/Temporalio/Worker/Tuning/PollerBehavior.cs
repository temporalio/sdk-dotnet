namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// Defines the behavior of a poller.
    /// </summary>
    public abstract class PollerBehavior
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PollerBehavior"/> class.
        /// </summary>
        internal PollerBehavior()
        {
        }

        /// <summary>
        /// A poller behavior that will attempt to poll as long as a slot is available, up to the
        /// provided maximum. Cannot be less than two for workflow tasks, or one for other tasks.
        /// </summary>
        public sealed class SimpleMaximum : PollerBehavior
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="PollerBehavior.SimpleMaximum"/> class.
            /// </summary>
            /// <param name="maximum">The maximum number of pollers at a time.</param>
            public SimpleMaximum(uint maximum = 5)
            {
                Maximum = maximum;
            }

            /// <summary>
            /// Gets maximum number of polls.
            /// </summary>
            public uint Maximum { get; }
        }

        /// <summary>
        /// A poller behavior that will automatically scale the number of pollers based on feedback
        /// from the server. A slot must be available before beginning polling.
        /// </summary>
        public sealed class Autoscaling : PollerBehavior
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="PollerBehavior.Autoscaling"/> class.
            /// </summary>
            /// <param name="minimum">At least this many poll calls will always be attempted (assuming slots are available).</param>
            /// <param name="maximum">At most this many poll calls will ever be open at once. Must be >= `minimum`.</param>
            /// <param name="initial">This many polls will be attempted initially before scaling kicks in. Must be between
            /// `minimum` and `maximum`.</param>
            public Autoscaling(uint minimum = 1, uint maximum = 100, uint initial = 5)
            {
                Minimum = minimum;
                Maximum = maximum;
                Initial = initial;
            }

            /// <summary>
            /// Gets the least number of poll calls that will be attempted (assuming slots are available).
            /// </summary>
            public uint Minimum { get; }

            /// <summary>
            /// Gets the maximum number of poll calls that will ever be open at once. Must be >= `minimum`.
            /// </summary>
            public uint Maximum { get; }

            /// <summary>
            /// Gets the number of polls that will be attempted initially before scaling kicks in. Must be
            /// between `minimum` and `maximum`.
            /// </summary>
            public uint Initial { get; }
        }
    }
}
