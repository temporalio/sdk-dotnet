using System;

namespace Temporalio.Activities
{
    /// <summary>
    /// Designate a method as an activity.
    /// </summary>
    /// <remarks>
    /// This attribute is not inherited, so if a base class method has this attribute, its override
    /// must too.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class ActivityAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityAttribute"/> class with the default
        /// name. See <see cref="Name" />.
        /// </summary>
        public ActivityAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityAttribute"/> class with the given
        /// name.
        /// </summary>
        /// <param name="name">Activity type name to use. See <see cref="Name" />.</param>
        public ActivityAttribute(string name) => Name = name;

        /// <summary>
        /// Gets the activity type name. If this is unset, it defaults to the unqualified method
        /// name (with "Async" trimmed off the end if present and the return type is a task).
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the activity is dynamic. If an activity is
        /// dynamic, it cannot by given a name in this attribute and the method must accept an array
        /// of <see cref="Converters.IRawValue" />.
        /// </summary>
        public bool Dynamic { get; set; }
    }
}