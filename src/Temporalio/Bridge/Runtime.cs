using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core-owned runtime.
    /// </summary>
    internal class Runtime : SafeHandle
    {
        private static readonly Func<ForwardedLog, Exception?, string> ForwardLogMessageFormatter =
            LogMessageFormatter;

        private readonly ILogger? forwardToLogger;
        private readonly bool forwardLoggerIncludeFields;

        /// <summary>
        /// Initializes a new instance of the <see cref="Runtime"/> class.
        /// </summary>
        /// <param name="options">Runtime options.</param>
        /// <exception cref="InvalidOperationException">Any internal core error.</exception>
        public Runtime(Temporalio.Runtime.TemporalRuntimeOptions options)
            : base(IntPtr.Zero, true)
        {
            using (var scope = new Scope())
            {
                unsafe
                {
                    // Setup forwarding logger
                    IntPtr? forwardingLogCallback = null;
                    if (options.Telemetry.Logging?.Forwarding is { } forwarding)
                    {
                        if (forwarding.Logger == null)
                        {
                            throw new ArgumentException("Must have logger on forwarding options");
                        }
                        forwardToLogger = forwarding.Logger;
                        forwardLoggerIncludeFields = forwarding.IncludeFields;
                        forwardingLogCallback = scope.FunctionPointer<Interop.ForwardedLogCallback>(OnLog);
                    }

                    // WARNING: It is important that this options is immediately passed to new
                    // because we have allocated a pointer for the custom meter which can only be
                    // freed on the Rust side on error
                    var runtimeOptions = options.ToInteropOptions(scope);
                    // Set log forwarding if enabled
                    if (forwardingLogCallback != null)
                    {
                        runtimeOptions.telemetry->logging->forward_to = forwardingLogCallback.Value;
                    }
                    var res = Interop.Methods.runtime_new(scope.Pointer(runtimeOptions));
                    // If it failed, copy byte array, free runtime and byte array. Otherwise just
                    // return runtime.
                    if (res.fail != null)
                    {
                        var message = ByteArrayRef.StrictUTF8.GetString(
                            res.fail->data,
                            (int)res.fail->size);
                        Interop.Methods.byte_array_free(res.runtime, res.fail);
                        Interop.Methods.runtime_free(res.runtime);
                        throw new InvalidOperationException(message);
                    }
                    Ptr = res.runtime;
                    SetHandle((IntPtr)Ptr);
                }
            }
            MetricMeter = new(() => new(this));
        }

        /// <inheritdoc />
        public override unsafe bool IsInvalid => false;

        /// <summary>
        /// Gets the pointer to the runtime.
        /// </summary>
        internal unsafe Interop.Runtime* Ptr { get; private init; }

        /// <summary>
        /// Gets the lazy metric meter for this runtime.
        /// </summary>
        internal Lazy<MetricMeter> MetricMeter { get; private init; }

        /// <summary>
        /// Free a byte array.
        /// </summary>
        /// <param name="byteArray">Byte array to free.</param>
        internal unsafe void FreeByteArray(Interop.ByteArray* byteArray)
        {
            Interop.Methods.byte_array_free(Ptr, byteArray);
        }

        /// <inheritdoc />
        protected override unsafe bool ReleaseHandle()
        {
            Interop.Methods.runtime_free(Ptr);
            return true;
        }

        private static string LogMessageFormatter(ForwardedLog state, Exception? error) =>
            state.ToString();

        private unsafe void OnLog(Interop.ForwardedLogLevel coreLevel, Interop.ForwardedLog* coreLog)
        {
            if (forwardToLogger is not { } logger)
            {
                return;
            }
            // Fortunately the Core log levels integers match .NET ones
            var level = (LogLevel)coreLevel;
            // Go no further if not enabled
            if (!logger.IsEnabled(level))
            {
                return;
            }
            // If the fields are requested, we will try to convert from JSON
            IDictionary<string, JsonElement>? fields = null;
            if (forwardLoggerIncludeFields)
            {
                var fieldsJson = ByteArrayRef.ToUtf8(Interop.Methods.forwarded_log_fields_json(coreLog));
                try
                {
                    fields = JsonSerializer.Deserialize<SortedDictionary<string, JsonElement>?>(fieldsJson);
                }
                catch (JsonException)
                {
                }
            }
            var log = new ForwardedLog(
                Level: level,
                Target: ByteArrayRef.ToUtf8(Interop.Methods.forwarded_log_target(coreLog)),
                Message: ByteArrayRef.ToUtf8(Interop.Methods.forwarded_log_message(coreLog)),
                TimestampMilliseconds: Interop.Methods.forwarded_log_timestamp_millis(coreLog),
                Fields: fields);
            logger.Log(level, 0, log, null, ForwardLogMessageFormatter);
        }
    }
}
