using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Temporalio.Runtime;

namespace Temporalio.Activities
{
    /// <summary>
    /// Definition of an activity.
    /// </summary>
    public class ActivityDefinition
    {
        private readonly Func<object?[], object?> invoker;

        private ActivityDefinition(
            string? name,
            Type returnType,
            IReadOnlyCollection<Type> parameterTypes,
            int requiredParameterCount,
            Func<object?[], object?> invoker,
            MethodInfo? methodInfo)
        {
            Name = name;
            ReturnType = returnType;
            ParameterTypes = parameterTypes;
            RequiredParameterCount = requiredParameterCount;
            this.invoker = invoker;
            MethodInfo = methodInfo;
        }

        /// <summary>
        /// Gets the activity name or null if workflow is dynamic.
        /// </summary>
        public string? Name { get; private init; }

        /// <summary>
        /// Gets the return type for the definition. This may be a Task for async activities. This
        /// is currently unused (callers are expected to provide the return type as needed).
        /// </summary>
        public Type ReturnType { get; private init; }

        /// <summary>
        /// Gets the parameter types for the definition. This is used by the activity worker to know
        /// what to deserialize input values into.
        /// </summary>
        public IReadOnlyCollection<Type> ParameterTypes { get; private init; }

        /// <summary>
        /// Gets the number of parameters required to be sent to <see cref="InvokeAsync" />.
        /// Activity invocation will fail if fewer are given.
        /// </summary>
        public int RequiredParameterCount { get; private init; }

        /// <summary>
        /// Gets a value indicating whether the activity is dynamic.
        /// </summary>
        public bool Dynamic => Name == null;

        /// <summary>
        /// Gets the <see cref="MethodInfo"/>. Will only have a value if one was used to create this <see cref="ActivityDefinition"/>.
        /// </summary>
        public MethodInfo? MethodInfo { get; private init; }

        /// <summary>
        /// Create an activity definition from a delegate. <see cref="Delegate.DynamicInvoke" /> is
        /// called on this delegate. The delegate must have an associated method and that method
        /// must have <see cref="ActivityAttribute" /> set on it.
        /// </summary>
        /// <param name="del">Delegate to create definition from.</param>
        /// <returns>Definition built from the delegate.</returns>
        public static ActivityDefinition Create(Delegate del)
        {
            if (del.Method == null)
            {
                throw new ArgumentException("Activities must have accessible methods");
            }
            return Create(del.Method, del.DynamicInvoke);
        }

        /// <summary>
        /// Create an activity definition manually from the given values.
        /// </summary>
        /// <param name="name">Name to use for the activity or null for dynamic.</param>
        /// <param name="returnType">Return type of the activity. This is currently unused.</param>
        /// <param name="parameterTypes">Parameter types for the invoker.</param>
        /// <param name="requiredParameterCount">Minimum number of parameters that must be provided
        /// to the invoker.</param>
        /// <param name="invoker">Function to call on activity invocation.</param>
        /// <returns>Definition built from the given pieces.</returns>
        public static ActivityDefinition Create(
            string? name,
            Type returnType,
            IReadOnlyCollection<Type> parameterTypes,
            int requiredParameterCount,
            Func<object?[], object?> invoker)
        {
            return Create(name, returnType, parameterTypes, requiredParameterCount, invoker, methodInfo: null);
        }

        /// <summary>
        /// Create an activity definition with an attributed method and a custom invoker.
        /// </summary>
        /// <param name="method">Activity method.</param>
        /// <param name="invoker">Invoker.</param>
        /// <returns>Definition for the activity.</returns>
        public static ActivityDefinition Create(MethodInfo method, Func<object?[], object?> invoker)
        {
            var attr = method.GetCustomAttribute<ActivityAttribute>(false) ??
                throw new ArgumentException($"{method} missing Activity attribute");
            return CreateWithoutAttribute(
                NameFromAttributed(method, attr), method, invoker);
        }

        /// <summary>
        /// Create an activity definition with a name, a method, and a custom invoker. This does not
        /// require/check the activity attribute. This is a helper for
        /// <see cref="Create(string?, Type, IReadOnlyCollection{Type}, int, Func{object?[], object?}, MethodInfo?)"/>
        /// that collects parameters and handles parameter defaults.
        /// </summary>
        /// <param name="name">Name to use for the activity or null for dynamic.</param>
        /// <param name="method">Activity method.</param>
        /// <param name="invoker">Invoker.</param>
        /// <returns>Definition for the activity.</returns>
        public static ActivityDefinition CreateWithoutAttribute(
            string? name, MethodInfo method, Func<object?[], object?> invoker)
        {
            if (method.ContainsGenericParameters)
            {
                throw new ArgumentException($"{method} contains generic parameters");
            }
            var parms = method.GetParameters();
            return Create(
                name,
                method.ReturnType,
                parms.Select(p => p.ParameterType).ToArray(),
                parms.Count(p => !p.HasDefaultValue),
                parameters => invoker.Invoke(ParametersWithDefaults(parms, parameters)),
                method);
        }

        /// <summary>
        /// Create all applicable activity definitions for the given type. At least one activity
        /// definition must exist.
        /// </summary>
        /// <typeparam name="T">Type with activity definitions.</typeparam>
        /// <param name="instance">Instance to invoke the activity definitions on. Must be non-null
        /// if any activities are non-static.</param>
        /// <returns>Collection of activity definitions on the type.</returns>
        public static IReadOnlyCollection<ActivityDefinition> CreateAll<T>(T? instance) =>
            CreateAll(typeof(T), instance);

        /// <summary>
        /// Create all applicable activity definitions for the given type. At least one activity
        /// definition must exist.
        /// </summary>
        /// <param name="type">Type with activity definitions.</param>
        /// <param name="instance">Instance to invoke the activity definitions on. Must be non-null
        /// if any activities are non-static.</param>
        /// <returns>Collection of activity definitions on the type.</returns>
        public static IReadOnlyCollection<ActivityDefinition> CreateAll(Type type, object? instance)
        {
            var ret = new List<ActivityDefinition>();
            foreach (var method in type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                var attr = method.GetCustomAttribute<ActivityAttribute>(true);
                if (attr == null)
                {
                    continue;
                }
                if (!method.IsStatic && instance == null)
                {
                    throw new InvalidOperationException(
                        $"Instance not provided, but activity method {method} is non-static");
                }
                ret.Add(Create(method, parameters => method.Invoke(instance, parameters)));
            }
            if (ret.Count == 0)
            {
                throw new ArgumentException($"No activities found on {type}", nameof(type));
            }
            return ret;
        }

        /// <summary>
        /// Invoke this activity with the given parameters. Before calling this, callers should
        /// have already validated that the parameters match <see cref="ParameterTypes" /> and there
        /// are at least <see cref="RequiredParameterCount" /> parameters. If the activity returns
        /// a Task, it is waited on and the result is extracted. If it is an untyped Task, the
        /// successful result will be Task&lt;ValueTuple&gt;.
        /// </summary>
        /// <param name="parameters">Parameters for the call.</param>
        /// <returns>Task for result.</returns>
        public async Task<object?> InvokeAsync(object?[] parameters)
        {
            // Have to unwrap and re-throw target invocation exception if present
            object? result;
            try
            {
                result = invoker.Invoke(parameters);
            }
            catch (TargetInvocationException e)
            {
                ExceptionDispatchInfo.Capture(e.InnerException!).Throw();
                // Unreachable
                throw new InvalidOperationException("Unreachable");
            }
            // If the result is a task, we need to await on it and use that result
            if (result is Task resultTask)
            {
                await resultTask.ConfigureAwait(false);
                // We have to use reflection to extract value if it's a Task<>
                var resultTaskType = resultTask.GetType();
                if (resultTaskType.IsGenericType)
                {
                    result = resultTaskType.GetProperty("Result")!.GetValue(resultTask);
                }
                else
                {
                    result = ValueTuple.Create();
                }
            }
            return result;
        }

        /// <summary>
        /// Gets the activity name for calling or fail if no attribute or if dynamic.
        /// </summary>
        /// <param name="method">Method to get name from.</param>
        /// <returns>Name.</returns>
        internal static string NameFromMethodForCall(MethodInfo method)
        {
            var attr = method.GetCustomAttribute<ActivityAttribute>(false) ??
                throw new ArgumentException($"{method} missing Activity attribute");
            return NameFromAttributed(method, attr) ??
                throw new ArgumentException(
                    $"{method} cannot be used directly since it is a dynamic activity");
        }

        private static object?[] ParametersWithDefaults(
            ParameterInfo[] paramInfos, object?[] parameters)
        {
            if (parameters.Length >= paramInfos.Length)
            {
                return parameters;
            }
            var ret = new List<object?>(parameters.Length);
            ret.AddRange(parameters);
            for (var i = parameters.Length; i < paramInfos.Length; i++)
            {
                ret.Add(paramInfos[i].DefaultValue);
            }
            return ret.ToArray();
        }

        private static string? NameFromAttributed(MethodInfo method, ActivityAttribute attr)
        {
            // Dynamic doesn't have name
            if (attr.Dynamic)
            {
                if (attr.Name != null)
                {
                    throw new ArgumentException(
                        "Activity ${method} cannot be dynamic and have custom name");
                }
                return null;
            }
            var name = attr.Name;
            if (name != null)
            {
                return name;
            }
            // Build name from method name
            name = method.Name;
            // Local functions are in the form <parent>g__name|other, so we will try to
            // extract the name
            var localBegin = name.IndexOf(">g__");
            if (localBegin > 0)
            {
                name = name.Substring(localBegin + 4);
                var localEnd = name.IndexOf('|');
                if (localEnd == -1)
                {
                    throw new ArgumentException($"Cannot parse name from local function {method}");
                }
                name = name.Substring(0, localEnd);
            }
            // Lambdas will have >b__ on them, but we just check for the angle bracket to
            // disallow any similar form including local functions we missed
            if (name.Contains("<"))
            {
                throw new ArgumentException(
                    $"{method} appears to be a lambda which must have a name given on the attribute");
            }
            if (typeof(Task).IsAssignableFrom(method.ReturnType) &&
                name.Length > 5 && name.EndsWith("Async"))
            {
                name = name.Substring(0, name.Length - 5);
            }
            return name;
        }

        private static ActivityDefinition Create(
            string? name,
            Type returnType,
            IReadOnlyCollection<Type> parameterTypes,
            int requiredParameterCount,
            Func<object?[], object?> invoker,
            MethodInfo? methodInfo)
        {
            if (name != null && name.StartsWith(TemporalRuntime.ReservedNamePrefix))
            {
                throw new ArgumentException(
                    $"Activity name {name} cannot start with {TemporalRuntime.ReservedNamePrefix}");
            }
            // If there is a null name, which means dynamic, there must only be one parameter type
            // and it must be varargs IRawValue
            if (name == null && (
                requiredParameterCount != 1 ||
                parameterTypes.SingleOrDefault() != typeof(Converters.IRawValue[])))
            {
                throw new ArgumentException(
                    $"Dynamic activity must accept a required array of IRawValue");
            }

            if (requiredParameterCount > parameterTypes.Count)
            {
                throw new ArgumentException(
                    $"Activity {name} has more required parameters than parameters",
                    nameof(requiredParameterCount));
            }
            foreach (var parameterType in parameterTypes)
            {
                if (parameterType.IsByRef)
                {
                    throw new ArgumentException(
                        $"Activity {name} has disallowed ref/out parameter");
                }
            }
            return new(name, returnType, parameterTypes, requiredParameterCount, invoker, methodInfo);
        }
    }
}
