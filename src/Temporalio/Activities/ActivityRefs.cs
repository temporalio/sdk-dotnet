namespace Temporalio.Activities
{
    /// <summary>
    /// Static helper for creating an instance of a type to reference its methods.
    /// </summary>
    public static class ActivityRefs
    {
        /// <summary>
        /// Create an instance of the given type. This should only be used to reference methods on,
        /// not to make any calls on. Instances returned may not be instantiated.
        /// </summary>
        /// <typeparam name="T">Type to create an instance of.</typeparam>
        /// <returns>Instance of this type.</returns>
        public static T Create<T>() => Refs.Create<T>();
    }
}