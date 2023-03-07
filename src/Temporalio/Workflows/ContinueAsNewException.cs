using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Temporalio.Exceptions;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Exception thrown by a workflow to continue as new. Use <c>Create</c> to create.
    /// </summary>
    public class ContinueAsNewException : TemporalException
    {
        private ContinueAsNewException(
            string workflow, IReadOnlyCollection<object?> args, ContinueAsNewOptions? options)
            : base("Continue as new")
        {
            Workflow = workflow;
            Args = args;
            Options = options;
        }

        /// <summary>
        /// Gets the workflow type name to continue as new with.
        /// </summary>
        public string Workflow { get; private init; }

        /// <summary>
        /// Gets the workflow arguments to continue as new with.
        /// </summary>
        public IReadOnlyCollection<object?> Args { get; private init; }

        /// <summary>
        /// Gets the continue as new options.
        /// </summary>
        public ContinueAsNewOptions? Options { get; private init; }

        /// <summary>
        /// Create a continue as new exception.
        /// </summary>
        /// <param name="workflow">Workflow run method reference.</param>
        /// <param name="options">Continue as new options.</param>
        /// <returns>Continue as new exception.</returns>
        public static ContinueAsNewException Create(
            Func<Task> workflow, ContinueAsNewOptions? options = null)
        {
            return new(
                WorkflowAttribute.Definition.FromRunMethod(workflow.Method).Name,
                Array.Empty<object?>(),
                options);
        }

        /// <summary>
        /// Create a continue as new exception.
        /// </summary>
        /// <typeparam name="T">Type of the workflow argument.</typeparam>
        /// <param name="workflow">Workflow run method reference.</param>
        /// <param name="arg">Workflow argument.</param>
        /// <param name="options">Continue as new options.</param>
        /// <returns>Continue as new exception.</returns>
        public static ContinueAsNewException Create<T>(
            Func<T, Task> workflow, T arg, ContinueAsNewOptions? options = null)
        {
            return new(
                WorkflowAttribute.Definition.FromRunMethod(workflow.Method).Name,
                new object?[] { arg },
                options);
        }

        /// <summary>
        /// Create a continue as new exception.
        /// </summary>
        /// <typeparam name="TResult">Type of the workflow result.</typeparam>
        /// <param name="workflow">Workflow run method reference.</param>
        /// <param name="options">Continue as new options.</param>
        /// <returns>Continue as new exception.</returns>
        public static ContinueAsNewException Create<TResult>(
            Func<Task<TResult>> workflow, ContinueAsNewOptions? options = null)
        {
            return new(
                WorkflowAttribute.Definition.FromRunMethod(workflow.Method).Name,
                Array.Empty<object?>(),
                options);
        }

        /// <summary>
        /// Create a continue as new exception.
        /// </summary>
        /// <typeparam name="T">Type of the workflow argument.</typeparam>
        /// <typeparam name="TResult">Type of the workflow result.</typeparam>
        /// <param name="workflow">Workflow run method reference.</param>
        /// <param name="arg">Workflow argument.</param>
        /// <param name="options">Continue as new options.</param>
        /// <returns>Continue as new exception.</returns>
        public static ContinueAsNewException Create<T, TResult>(
            Func<T, Task<TResult>> workflow, T arg, ContinueAsNewOptions? options = null)
        {
            return new(
                WorkflowAttribute.Definition.FromRunMethod(workflow.Method).Name,
                new object?[] { arg },
                options);
        }

        /// <summary>
        /// Create a continue as new exception.
        /// </summary>
        /// <param name="workflow">Workflow type name.</param>
        /// <param name="args">Workflow arguments.</param>
        /// <param name="options">Continue as new options.</param>
        /// <returns>Continue as new exception.</returns>
        public static ContinueAsNewException Create(
            string workflow, object?[] args, ContinueAsNewOptions? options = null)
        {
            return new(workflow, args, options);
        }
    }
}