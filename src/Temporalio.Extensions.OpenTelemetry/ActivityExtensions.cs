using System;
using System.Diagnostics;
using OpenTelemetry.Trace;

namespace Temporalio.Extensions.OpenTelemetry
{
    /// <summary>
    /// Temporal extensions to <see cref="Activity" />.
    /// </summary>
    public static class ActivityExtensions
    {
        /// <summary>
        /// Records an exception together with setting a status appropriately.
        /// </summary>
        /// <param name="activity">An activity being updated.</param>
        /// <param name="exception">An exception used to update the <paramref name="activity"/> accordingly.</param>
        public static void RecordExceptionWithStatus(this Activity activity, Exception exception)
        {
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.RecordException(exception);
        }
    }
}