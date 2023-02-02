using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Temporalio.Workflow
{
    /// <summary>
    /// Designate a type as a workflow.
    /// </summary>
    /// <remarks>
    /// This attribute is not inherited, so if a base class has this attribute the registered
    /// subclass must have it too. Workflows must have a no-arg constructor unless there is a
    /// constructor with <see cref="WorkflowInitAttribute" />. All workflows must have a single
    /// <see cref="WorkflowRunAttribute" />.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
    public class WorkflowAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowAttribute"/> class with the default
        /// name.
        /// </summary>
        /// <seealso cref="Name" />
        public WorkflowAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowAttribute"/> class with the given
        /// name.
        /// </summary>
        /// <param name="name">Workflow type name to use.</param>
        public WorkflowAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Gets or sets the workflow type name. If this is unset, it defaults to the unqualified
        /// type name. If the type is an interface and the first character is a capital "I" followed
        /// by another capital letter, the "I" is trimmed when creating the default name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Internal representation of a workflow definition.
        /// </summary>
        /// <param name="Name">Workflow type name.</param>
        /// <param name="Type">Type of the workflow.</param>
        /// <param name="RunMethod">Workflow entry point.</param>
        /// <param name="Instantiable">Whether it is an instantiable definition.</param>
        /// <param name="InitConstructor">Optional constructor accepting args.</param>
        /// <param name="Signals">Workflow signals.</param>
        /// <param name="Queries">Workflow queries.</param>
        internal record Definition(
            string Name,
            Type Type,
            MethodInfo RunMethod,
            bool Instantiable,
            ConstructorInfo? InitConstructor,
            IReadOnlyDictionary<string, WorkflowSignalAttribute.Definition> Signals,
            IReadOnlyDictionary<string, WorkflowQueryAttribute.Definition> Queries)
        {
            private static readonly ConcurrentDictionary<Type, Definition> Definitions = new();

            /// <summary>
            /// Get a workflow definition for the given type or fail. The result is cached.
            /// </summary>
            /// <param name="type">Type to get definition for.</param>
            /// <returns>Definition for the type.</returns>
            public static Definition FromType(Type type)
            {
                return Definitions.GetOrAdd(type, CreateFromType);
            }

            /// <summary>
            /// Get a workflow definition for the given workflow run method or fail. The result is
            /// cached.
            /// </summary>
            /// <param name="runMethod">Method with a <see cref="WorkflowRunAttribute" />.</param>
            /// <returns>Definition for the type.</returns>
            public static Definition FromRunMethod(MethodInfo runMethod)
            {
                if (runMethod.GetCustomAttribute<WorkflowRunAttribute>() == null)
                {
                    throw new ArgumentException($"{runMethod} missing WorkflowRun attribute");
                }
                // We intentionally use reflected type because we don't allow inheritance of run methods
                // in any way, they must be explicitly defined on the type
                return FromType(runMethod.ReflectedType ??
                    throw new ArgumentException($"{runMethod} has no reflected type"));
            }

            private static Definition CreateFromType(Type type)
            {
                const BindingFlags bindingFlagsAny =
                    BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.Public | BindingFlags.NonPublic;
                // Unwrap the type
                type = Refs.GetUnproxiedType(type);

                // Get the main attribute, but throw immediately if it is not present
                var attr = type.GetCustomAttribute<WorkflowAttribute>(false) ??
                    throw new ArgumentException($"{type} missing Workflow attribute");

                // We will keep track of errors and only throw an aggregate at the end
                var errs = new List<string>();

                // Check constructors. We intentionally fetch non-public too to make sure the init
                // attribute isn't set on them.
                ConstructorInfo? initConstructor = null;
                var constructors = type.GetConstructors(bindingFlagsAny);
                var hasParameterlessConstructor = constructors.Length == 0;
                foreach (var constructor in constructors)
                {
                    if (constructor.GetParameters().Length == 0 && constructor.IsPublic)
                    {
                        hasParameterlessConstructor = true;
                    }
                    var initAttr = constructor.GetCustomAttribute<WorkflowInitAttribute>(false);
                    if (initAttr != null)
                    {
                        if (initConstructor != null)
                        {
                            errs.Add($"WorkflowInit on multiple: {constructor} and {initConstructor}");
                        }
                        else if (!constructor.IsPublic)
                        {
                            errs.Add($"WorkflowInit on non-public {constructor}");
                        }
                        else
                        {
                            initConstructor = constructor;
                        }
                    }
                }
                var instantiable = !type.IsInterface &&
                    (hasParameterlessConstructor || initConstructor == null);

                // Find and validate run, signal, and query methods. We intentionally fetch
                // non-public too to make sure attributes aren't set on them.
                MethodInfo? runMethod = null;
                var signals = new Dictionary<string, WorkflowSignalAttribute.Definition>();
                var queries = new Dictionary<string, WorkflowQueryAttribute.Definition>();
                foreach (var method in type.GetMethods(bindingFlagsAny))
                {
                    var runAttr = method.GetCustomAttribute<WorkflowRunAttribute>(false);
                    if (runAttr != null)
                    {
                        if (method.DeclaringType != type)
                        {
                            errs.Add($"WorkflowRun on {method} must be declared on {type}, not inherited from {method.DeclaringType}");
                        }
                        else if (runMethod != null)
                        {
                            errs.Add($"WorkflowRun on multiple: {method} and {runMethod}");
                        }
                        else if (!method.IsPublic)
                        {
                            errs.Add($"WorkflowRun on non-public {method}");
                        }
                        else if (!typeof(Task).IsAssignableFrom(method.ReturnType))
                        {
                            errs.Add($"WorkflowRun method {method} must return an instance of Task");
                        }
                        else if (initConstructor != null &&
                            !method.GetParameters().Select(p => p.ParameterType).SequenceEqual(
                                initConstructor.GetParameters().Select(p => p.ParameterType)))
                        {
                            errs.Add($"WorkflowRun on {method} must match parameter types of WorkflowInit on {initConstructor}");
                        }
                        else
                        {
                            runMethod = method;
                        }
                    }
                    if (method.IsDefined(typeof(WorkflowSignalAttribute), false))
                    {
                        try
                        {
                            var defn = WorkflowSignalAttribute.Definition.FromMethod(method);
                            if (signals.ContainsKey(defn.Name))
                            {
                                errs.Add($"{type} has more than one signal named {defn.Name}");
                            }
                            signals[defn.Name] = defn;
                        }
                        catch (ArgumentException e)
                        {
                            errs.Add(e.Message);
                        }
                    }
                    else if (IsDefinedOnBase<WorkflowSignalAttribute>(method))
                    {
                        errs.Add($"WorkflowSignal on base definition of {method} but not override");
                    }
                    if (method.IsDefined(typeof(WorkflowQueryAttribute), false))
                    {
                        try
                        {
                            var defn = WorkflowQueryAttribute.Definition.FromMethod(method);
                            if (queries.ContainsKey(defn.Name))
                            {
                                errs.Add($"{type} has more than one query named {defn.Name}");
                            }
                            queries[defn.Name] = defn;
                        }
                        catch (ArgumentException e)
                        {
                            errs.Add(e.Message);
                        }
                    }
                    else if (IsDefinedOnBase<WorkflowQueryAttribute>(method))
                    {
                        errs.Add($"WorkflowQuery on base definition of {method} but not override");
                    }
                }
                if (runMethod == null)
                {
                    errs.Add($"{type} does not have a valid WorkflowRun method");
                }

                // If there are any errors, throw
                if (errs.Count > 0)
                {
                    // TODO(cretz): Ok to use aggregate exception here or should I just
                    // comma-delimit into a single message or something?
                    throw new AggregateException(errs.Select(err => new ArgumentException(err)));
                }

                // Use given name or default
                var name = attr.Name;
                if (name == null)
                {
                    name = type.Name;
                    // If type is an interface and name has a leading I followed by another capital, trim it
                    // off
                    if (type.IsInterface && name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]))
                    {
                        name = name.Substring(1);
                    }
                }
                return new Definition(
                    Name: name,
                    Type: type,
                    RunMethod: runMethod!,
                    Instantiable: instantiable,
                    InitConstructor: initConstructor,
                    Signals: signals,
                    Queries: queries);
            }

            private static bool IsDefinedOnBase<T>(MethodInfo method)
                where T : Attribute
            {
                while (true)
                {
                    var baseDef = method.GetBaseDefinition();
                    if (baseDef == method)
                    {
                        return false;
                    }
                    else if (baseDef.IsDefined(typeof(T), false))
                    {
                        return true;
                    }
                    method = baseDef;
                }
            }
        }
    }
}
