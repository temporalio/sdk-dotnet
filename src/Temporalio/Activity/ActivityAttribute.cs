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
        public ActivityAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Gets the activity type name. If this is unset, it defaults to the unqualified method
        /// name (with "Async" trimmed off the end if present and the return type is a task).
        /// </summary>
        public string? Name { get; }

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
                    throw new ArgumentException($"{del.Method} missing Activity attribute");

                // Check parameters. We disallow ref/out and varargs, but we do allow defaults.
                foreach (var param in del.Method.GetParameters())
                {
                    if (param.ParameterType.IsByRef)
                    {
                        throw new ArgumentException($"{del.Method} has disallowed ref/out parameter");
                    }
                }

                // We don't enforce anything about visibility or return type
                var name = attr.Name;
                if (name == null)
                {
                    name = del.Method.Name;
                    // Local functions are in the form <parent>g__name|other, so we will try to
                    // extract the name
                    var localBegin = name.IndexOf(">g__");
                    if (localBegin > 0)
                    {
                        name = name.Substring(localBegin + 4);
                        var localEnd = name.IndexOf('|');
                        if (localEnd == -1)
                        {
                            throw new ArgumentException(
                                $"Cannot parse name from local function {del.Method}");
                        }
                        name = name.Substring(0, localEnd);
                    }
                    // Lambdas will have >b__ on them, but we just check for the angle bracket to
                    // disallow any similar form including local functions we missed
                    if (name.Contains("<"))
                    {
                        throw new ArgumentException(
                            $"{del.Method} appears to be a lambda which must have a name given on the attribute");
                    }
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