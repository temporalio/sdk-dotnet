using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

namespace Temporalio.Activity
{
    /// <summary>
    /// Designate a method as a workflow.
    /// </summary>
    /// <remarks>
    /// This attribute is not inherited, so if a base class method has this attribute, its override
    /// must too.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class ActivityAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityAttribute"/> class with the default
        /// name.
        /// </summary>
        /// <seealso cref="Name" />
        public ActivityAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityAttribute"/> class with the given
        /// name.
        /// </summary>
        /// <param name="name">Activity type name to use.</param>
        public ActivityAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Gets or sets the activity type name. If this is unset, it defaults to the unqualified
        /// method name (with "Async" trimmed off the end if present and the return type is a
        /// task).
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Internal representation of an activity definition.
        /// </summary>
        /// <param name="Name">Activity type name.</param>
        /// <param name="Delegate">Activity delegate.</param>
        internal record Definition(string Name, Delegate Delegate)
        {
            private static readonly ConcurrentDictionary<Delegate, Definition> Definitions = new();

            /// <summary>
            /// Get an activity definition from a delegate or fail. The result is cached.
            /// </summary>
            /// <param name="del">Activity delegate.</param>
            /// <returns>Activity definition.</returns>
            public static Definition FromDelegate(Delegate del)
            {
                return Definitions.GetOrAdd(del, CreateFromDelegate);
            }

            private static Definition CreateFromDelegate(Delegate del)
            {
                var attr = del.Method.GetCustomAttribute<ActivityAttribute>(false) ??
                    throw new ArgumentException($"{del} missing Activity attribute");

                // Check parameters. We disallow ref/out and varargs, but we do allow defaults.
                foreach (var param in del.Method.GetParameters())
                {
                    if (param.ParameterType.IsByRef)
                    {
                        throw new ArgumentException($"{del} has disallowed ref/out parameter");
                    }
                }

                // We don't enforce anything about visibility or return type
                var name = attr.Name;
                if (name == null)
                {
                    name = del.Method.Name;
                    if (typeof(Task).IsAssignableFrom(del.Method.ReturnType) &&
                        name.Length > 5 && name.EndsWith("Async"))
                    {
                        name = name.Substring(0, name.Length - 5);
                    }
                }
                return new(name, del);
            }
        }
    }
}