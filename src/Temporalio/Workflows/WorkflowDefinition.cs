using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Definition of an workflow.
    /// </summary>
    public class WorkflowDefinition
    {
        private static readonly ConcurrentDictionary<Type, WorkflowDefinition> Definitions = new();

        private WorkflowDefinition(
            string name,
            Type type,
            MethodInfo runMethod,
            bool instantiable,
            ConstructorInfo? initConstructor,
            IReadOnlyDictionary<string, WorkflowSignalDefinition> signals,
            IReadOnlyDictionary<string, WorkflowQueryDefinition> queries)
        {
            Name = name;
            Type = type;
            RunMethod = runMethod;
            Instantiable = instantiable;
            InitConstructor = initConstructor;
            Signals = signals;
            Queries = queries;
        }

        /// <summary>
        /// Gets the workflow name.
        /// </summary>
        public string Name { get; private init; }

        /// <summary>
        /// Gets the workflow type.
        /// </summary>
        public Type Type { get; private init; }

        /// <summary>
        /// Gets the workflow entry point.
        /// </summary>
        public MethodInfo RunMethod { get; private init; }

        /// <summary>
        /// Gets a value indicating whether the workflow type can be created and used in a worker.
        /// </summary>
        public bool Instantiable { get; private init; }

        /// <summary>
        /// Gets the alternative constructor to use if not using the default.
        /// </summary>
        public ConstructorInfo? InitConstructor { get; private init; }

        /// <summary>
        /// Gets the signals for the workflow.
        /// </summary>
        public IReadOnlyDictionary<string, WorkflowSignalDefinition> Signals { get; private init; }

        /// <summary>
        /// Gets the queries for the workflow.
        /// </summary>
        public IReadOnlyDictionary<string, WorkflowQueryDefinition> Queries { get; private init; }

        /// <summary>
        /// Get a workflow definition for the given type or fail. The result is cached.
        /// </summary>
        /// <param name="type">Type to get definition for.</param>
        /// <returns>Definition for the type.</returns>
        public static WorkflowDefinition FromType(Type type)
        {
            return Definitions.GetOrAdd(type, CreateFromType);
        }

        /// <summary>
        /// Get a workflow definition for the given workflow run method or fail. The result is
        /// cached.
        /// </summary>
        /// <param name="runMethod">Method with a <see cref="WorkflowRunAttribute" />.</param>
        /// <returns>Definition for the type.</returns>
        public static WorkflowDefinition FromRunMethod(MethodInfo runMethod)
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

        /// <summary>
        /// Creates a workflow definition from an explicit name and type. Most users should use
        /// <see cref="FromType" /> with attributes instead.
        /// </summary>
        /// <param name="name">Workflow name.</param>
        /// <param name="type">Workflow type.</param>
        /// <returns>Workflow definition.</returns>
        public static WorkflowDefinition CreateWithoutAttribute(string name, Type type)
        {
            const BindingFlags bindingFlagsAny =
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic;
            // Unwrap the type
            type = Refs.GetUnderlyingType(type);

            // Get the main attribute, but throw immediately if it is not present
            var attr = type.GetCustomAttribute<WorkflowAttribute>(false) ??
                throw new ArgumentException($"{type} missing Workflow attribute");

            // We will keep track of errors and only throw an aggregate at the end
            var errs = new List<string>();

            // No generics allowed currently
            if (type.GenericTypeArguments.Length > 0)
            {
                errs.Add($"{type} has generic type arguments");
            }

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
            var signals = new Dictionary<string, WorkflowSignalDefinition>();
            var queries = new Dictionary<string, WorkflowQueryDefinition>();
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
                    else if (method.IsStatic)
                    {
                        errs.Add($"WorkflowRun on static {method}");
                    }
                    else if (method.ContainsGenericParameters)
                    {
                        errs.Add($"{method} with WorkflowRun contains generic parameters");
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
                        var defn = WorkflowSignalDefinition.FromMethod(method);
                        if (signals.ContainsKey(defn.Name))
                        {
                            errs.Add($"{type} has more than one signal named {defn.Name}");
                        }
                        else if (method.ContainsGenericParameters)
                        {
                            errs.Add($"{method} with WorkflowSignal contains generic parameters");
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
                        var defn = WorkflowQueryDefinition.FromMethod(method);
                        if (queries.ContainsKey(defn.Name))
                        {
                            errs.Add($"{type} has more than one query named {defn.Name}");
                        }
                        else if (method.ContainsGenericParameters)
                        {
                            errs.Add($"{method} with WorkflowQuery contains generic parameters");
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

            return new(
                name: name,
                type: type,
                runMethod: runMethod!,
                instantiable: instantiable,
                initConstructor: initConstructor,
                signals: signals,
                queries: queries);
        }

        private static WorkflowDefinition CreateFromType(Type type)
        {
            // Unwrap the type
            type = Refs.GetUnderlyingType(type);

            // Get the main attribute, but throw immediately if it is not present
            var attr = type.GetCustomAttribute<WorkflowAttribute>(false) ??
                throw new ArgumentException($"{type} missing Workflow attribute");

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

            return CreateWithoutAttribute(name, type);
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