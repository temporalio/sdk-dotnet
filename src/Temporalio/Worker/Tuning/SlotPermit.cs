namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// A permit to use a slot for a workflow/activity/local activity task.
    /// </summary>
    /// <remarks>
    /// WARNING: Custom slot suppliers are currently experimental.
    /// </remarks>
    public class SlotPermit
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SlotPermit"/> class with no associated data.
        /// </summary>
        public SlotPermit()
        {
            UserData = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SlotPermit"/> class.
        /// </summary>
        /// <param name="data">Data associated with the permit.</param>
        public SlotPermit(object data)
        {
            UserData = data;
        }

        /// <summary>
        /// Gets data associated with the permit.
        /// </summary>
        public object? UserData { get; init; }
    }
}
