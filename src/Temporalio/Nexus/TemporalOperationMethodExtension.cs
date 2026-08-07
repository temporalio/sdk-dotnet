using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using NexusRpc;
using NexusRpc.Handlers;

namespace Temporalio.Nexus
{
    /// <summary>
    /// NexusRpc <see cref="IMethodExtension"/> that recognizes <see cref="TemporalOperationAttribute"/>
    /// methods and builds a <see cref="TemporalOperationHandler{TInput, TResult}"/> for each.
    /// </summary>
    internal sealed class TemporalOperationMethodExtension : IMethodExtension
    {
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static readonly TemporalOperationMethodExtension Instance = new();

        /// <summary>
        /// Convenience collection wrapping the singleton for passing to
        /// <c>ServiceHandlerInstance.FromInstance</c>.
        /// </summary>
        public static readonly IReadOnlyCollection<IMethodExtension> Extensions =
            new IMethodExtension[] { Instance };

        /// <inheritdoc/>
        public IOperationHandler<object?, object?>? Extract(
            object instance, MethodInfo method, OperationDefinition operationDefinition)
        {
            if (method.GetCustomAttribute<TemporalOperationAttribute>() == null)
            {
                return null;
            }
            // Validate the method and build a factory once, then bind it to the fixed instance.
            // The same factory is reused per-call (with a fresh instance) by the hosting/DI path.
            var handlerFactory = CreateInstanceHandlerFactory(method, operationDefinition);
            return handlerFactory(instance);
        }

        /// <summary>
        /// Validate a <see cref="TemporalOperationAttribute"/> method against the operation it
        /// handles and produce a factory that builds a (generic-wrapped) operation handler for a
        /// given service-handler instance.
        /// </summary>
        /// <remarks>
        /// The returned factory is backed by a single compiled delegate that takes the service
        /// instance as a parameter (rather than a captured constant). This lets the fixed-instance
        /// path (<see cref="Extract"/>) bind one instance up front while the hosting/dependency
        /// injection path resolves a fresh instance from a service scope on every start and cancel
        /// call — giving <c>[TemporalOperation]</c> handlers the same DI behavior as activities.
        /// </remarks>
        /// <param name="method">The <c>[TemporalOperation]</c> method.</param>
        /// <param name="opDef">Operation the method handles, matched by method name.</param>
        /// <returns>A factory from service instance to operation handler.</returns>
        internal static Func<object, IOperationHandler<object?, object?>>
            CreateInstanceHandlerFactory(MethodInfo method, OperationDefinition opDef)
        {
            if (!method.IsPublic)
            {
                throw new ArgumentException(
                    $"[TemporalOperation] method {method.DeclaringType}.{method.Name} must be public");
            }
            if (method.IsStatic)
            {
                throw new ArgumentException(
                    $"[TemporalOperation] method {method.DeclaringType}.{method.Name} must not be static");
            }

            // Treat void-like types (void, NoValue, ValueTuple) as "no value" for both input and
            // result, matching NexusRpc's own void normalization.
            var hasInputParam = !NoValue.IsVoidType(opDef.InputType);
            var inputType = hasInputParam ? opDef.InputType : typeof(NoValue);
            var resultType = NoValue.IsVoidType(opDef.OutputType) ? typeof(NoValue) : opDef.OutputType;

            // Expected: Task<TemporalOperationResult<TResult>> Method(
            //     TemporalOperationStartContext, ITemporalNexusClient[, TInput])
            var expectedReturn = typeof(Task<>).MakeGenericType(
                typeof(TemporalOperationResult<>).MakeGenericType(resultType));
            if (method.ReturnType != expectedReturn)
            {
                throw new ArgumentException(
                    $"[TemporalOperation] method {method.DeclaringType}.{method.Name} must return " +
                    $"{FormatTypeName(expectedReturn)}; got {FormatTypeName(method.ReturnType)}");
            }

            var parameters = method.GetParameters();
            var expectedParamCount = hasInputParam ? 3 : 2;
            if (parameters.Length != expectedParamCount ||
                parameters[0].ParameterType != typeof(TemporalOperationStartContext) ||
                parameters[1].ParameterType != typeof(ITemporalNexusClient) ||
                (hasInputParam && parameters[2].ParameterType != inputType))
            {
                var expected = hasInputParam
                    ? $"(TemporalOperationStartContext, ITemporalNexusClient, {FormatTypeName(inputType)})"
                    : "(TemporalOperationStartContext, ITemporalNexusClient)";
                throw new ArgumentException(
                    $"[TemporalOperation] method {method.DeclaringType}.{method.Name} must accept " +
                    $"parameters {expected}");
            }

            return BuildInstanceHandlerFactory(
                method, inputType, resultType, expectedReturn, hasInputParam);
        }

        /// <summary>
        /// Compile a delegate that, given a service-handler instance, builds a generic-wrapped
        /// <see cref="TemporalOperationHandler{TInput, TResult}"/> whose start function invokes the
        /// <c>[TemporalOperation]</c> method on that instance.
        /// </summary>
        private static Func<object, IOperationHandler<object?, object?>> BuildInstanceHandlerFactory(
            MethodInfo method,
            Type inputType,
            Type resultType,
            Type expectedReturn,
            bool hasInputParam)
        {
            // Func<TemporalOperationStartContext, ITemporalNexusClient, TInput,
            //     Task<TemporalOperationResult<TResult>>>
            var startFuncType = typeof(Func<,,,>).MakeGenericType(
                typeof(TemporalOperationStartContext),
                typeof(ITemporalNexusClient),
                inputType,
                expectedReturn);
            var handlerType = typeof(TemporalOperationHandler<,>).MakeGenericType(inputType, resultType);
            var handlerCtor = handlerType.GetConstructor(new[] { startFuncType })!;
            var ioHandlerType = typeof(IOperationHandler<,>).MakeGenericType(inputType, resultType);
            var wrapMethod = typeof(OperationHandler).GetMethod(
                nameof(OperationHandler.WrapAsGenericHandler),
                new[] { typeof(object), typeof(Type) })!;

            // instance => WrapAsGenericHandler(
            //     new TemporalOperationHandler<TInput, TResult>(
            //         (ctx, client, input) => ((TDeclaring)instance).Method(ctx, client[, input])),
            //     ioHandlerType)
            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var typedInstance = Expression.Convert(instanceParam, method.DeclaringType!);
            var ctxParam = Expression.Parameter(typeof(TemporalOperationStartContext), "ctx");
            var clientParam = Expression.Parameter(typeof(ITemporalNexusClient), "client");
            var inputParam = Expression.Parameter(inputType, "input");
            var call = hasInputParam
                ? Expression.Call(typedInstance, method, ctxParam, clientParam, inputParam)
                : Expression.Call(typedInstance, method, ctxParam, clientParam);
            var startLambda = Expression.Lambda(startFuncType, call, ctxParam, clientParam, inputParam);
            var newHandler = Expression.New(handlerCtor, startLambda);
            var wrapCall = Expression.Call(
                wrapMethod,
                Expression.Convert(newHandler, typeof(object)),
                Expression.Constant(ioHandlerType, typeof(Type)));
            var factoryLambda = Expression.Lambda<Func<object, IOperationHandler<object?, object?>>>(
                wrapCall, instanceParam);

            // Prefer interpretation over full IL compilation: it is far cheaper for these
            // single-use lambdas by avoiding Reflection.Emit + JIT work. The compile targets
            // for net470 and lower lack the overload.
#if NETSTANDARD2_0 || NETCOREAPP || NET471_OR_GREATER
            return factoryLambda.Compile(true);
#else
            return factoryLambda.Compile();
#endif
        }

        private static string FormatTypeName(Type type)
        {
            if (!type.IsGenericType)
            {
                return type.Name;
            }
            var name = type.Name;
            var backtick = name.IndexOf('`');
            if (backtick >= 0)
            {
                name = name.Substring(0, backtick);
            }
            var args = string.Join(", ", type.GetGenericArguments().Select(FormatTypeName));
            return $"{name}<{args}>";
        }
    }
}
