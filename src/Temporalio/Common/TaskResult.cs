using System;
using System.Threading.Tasks;

namespace Temporalio.Common
{
    /// <summary>
    /// Extracts the result of a completed task returned by user code.
    /// </summary>
    internal static class TaskResult
    {
        /// <summary>
        /// Try to get the result of a completed task based on the declared return type of the
        /// method that produced it.
        /// </summary>
        /// <param name="completedTask">Task that has already completed successfully.</param>
        /// <param name="declaredReturnType">Declared return type of the method that returned the
        /// task.</param>
        /// <param name="result">Result if there is one.</param>
        /// <returns>True if the task has a result.</returns>
        internal static bool TryGetResult(Task completedTask, Type declaredReturnType, out object? result)
        {
            // The runtime type cannot be trusted when the method is declared as a non-generic
            // Task: it may forward a Task<T> whose value the caller never asked for, and even a
            // plain async method's task is a generic state machine box over VoidTaskResult.
            var taskType = completedTask.GetType();
            if (declaredReturnType == typeof(Task) ||
                !taskType.IsGenericType ||
                taskType.GetProperty("Result") is not { } resultProperty)
            {
                result = null;
                return false;
            }
            result = resultProperty.GetValue(completedTask);
            return true;
        }
    }
}
