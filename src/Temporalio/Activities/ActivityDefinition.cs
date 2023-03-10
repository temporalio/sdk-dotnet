using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

namespace Temporalio.Activities
{
    /// <summary>
    /// Definition of an activity.
    /// </summary>
    public class ActivityDefinition
    {
        private static readonly ConcurrentDictionary<Delegate, ActivityDefinition> Definitions = new();

        private ActivityDefinition(string name, Delegate del)
        {
            Name = name;
            Delegate = del;
        }

        /// <summary>
        /// Gets the activity name.
        /// </summary>
        public string Name { get; private init; }

        /// <summary>
        /// Gets the activity delegate.
        /// </summary>
        public Delegate Delegate { get; private init; }

        /// <summary>
        /// Get an activity definition from a delegate containing <see cref="ActivityAttribute" />
        /// or fail if invalid. The result is cached.
        /// </summary>
        /// <param name="del">Activity delegate.</param>
        /// <returns>Activity definition.</returns>
        /// <remarks>
        /// Activity delegates cannot have any <c>ref</c> or <c>out</c> parameters.
        /// </remarks>
        public static ActivityDefinition FromDelegate(Delegate del)
        {
            return Definitions.GetOrAdd(del, CreateFromDelegate);
        }

        /// <summary>
        /// Creates an activity definition from an explicit name and delegate. Most users should use
        /// <see cref="FromDelegate" /> with attributes instead.
        /// </summary>
        /// <param name="name">Activity name.</param>
        /// <param name="del">Activity delegate.</param>
        /// <returns>Activity definition.</returns>
        /// <seealso cref="FromDelegate" />
        public static ActivityDefinition CreateWithoutAttribute(string name, Delegate del)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Name required for activity");
            }
            else if (del.Method == null)
            {
                throw new ArgumentException("Activities must have accessible methods");
            }
            // Check parameters. We disallow ref/out and varargs, but we do allow defaults. We don't
            // enforce anything about visibility or return type.
            foreach (var param in del.Method.GetParameters())
            {
                if (param.ParameterType.IsByRef)
                {
                    throw new ArgumentException($"{del.Method} has disallowed ref/out parameter");
                }
            }
            return new(name, del);
        }

        private static ActivityDefinition CreateFromDelegate(Delegate del)
        {
            if (del.Method == null)
            {
                throw new ArgumentException("Activities must have accessible methods");
            }
            var attr = del.Method.GetCustomAttribute<ActivityAttribute>(false) ??
                throw new ArgumentException($"{del.Method} missing Activity attribute");

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
            return CreateWithoutAttribute(name, del);
        }
    }
}