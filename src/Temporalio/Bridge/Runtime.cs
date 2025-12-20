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

        private bool forwardLoggerIncludeFields;
        private GCHandle? forwardLoggerCallback;
        private ILogger? forwardLogger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Runtime"/> class.
        /// </summary>
        /// <param name="options">Runtime options.</param>
        /// <exception cref="InvalidOperationException">Any internal core error.</exception>
        public Runtime(Temporalio.Runtime.TemporalRuntimeOptions options)
            : base(IntPtr.Zero, true)
        {
            Scope.WithScope(scope =>
            {
                unsafe
                {
                    // Setup forwarding logger
                    if (options.Telemetry.Logging?.Forwarding is { } forwarding)
                    {
                        if (forwarding.Logger == null)
                        {
                            throw new ArgumentException("Must have logger on forwarding options");
                        }
                        forwardLogger = forwarding.Logger;
                        forwardLoggerIncludeFields = forwarding.IncludeFields;
                    }

                    // WARNING: It is important that this options is immediately passed to new
                    // because we have allocated a pointer for the custom meter which can only be
                    // freed on the Rust side on error
                    var runtimeOptions = options.ToInteropOptions(scope);
                    // Set log forwarding if enabled
                    if (forwardLogger != null)
                    {
                        forwardLoggerCallback = GCHandle.Alloc(new Interop.TemporalCoreForwardedLogCallback(OnLog));
                        runtimeOptions.telemetry->logging->forward_to =
                            Marshal.GetFunctionPointerForDelegate(forwardLoggerCallback.Value.Target!);
                    }
                    var res = Interop.Methods.temporal_core_runtime_new(scope.Pointer(runtimeOptions));
                    // If it failed, copy byte array, free runtime and byte array. Otherwise just
                    // return runtime.
                    if (res.fail != null)
                    {
                        var message = ByteArrayRef.StrictUTF8.GetString(
                            res.fail->data,
                            (int)res.fail->size);
                        Interop.Methods.temporal_core_byte_array_free(res.runtime, res.fail);
                        Interop.Methods.temporal_core_runtime_free(res.runtime);
                        throw new InvalidOperationException(message);
                    }
                    Ptr = res.runtime;
                    SetHandle((IntPtr)Ptr);
                }
            });
            MetricMeter = new(() => Bridge.MetricMeter.CreateFromRuntime(this));
        }

        /// <inheritdoc />
        public override unsafe bool IsInvalid => Ptr == null;

        /// <summary>
        /// Gets the pointer to the runtime.
        /// </summary>
        internal unsafe Interop.TemporalCoreRuntime* Ptr { get; private set; }

        /// <summary>
        /// Gets the lazy metric meter for this runtime. Can be null.
        /// </summary>
        internal Lazy<MetricMeter?> MetricMeter { get; private init; }

        /// <summary>
        /// Read a JSON object into string keys and raw JSON values.
        /// </summary>
        /// <param name="bytes">Byte span.</param>
        /// <returns>Keys and raw values or null.</returns>
        internal static unsafe IReadOnlyDictionary<string, string>? ReadJsonObjectToRawValues(
            ReadOnlySpan<byte> bytes)
        {
            var reader = new Utf8JsonReader(bytes);
            // Expect start object
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return null;
            }
            // Property names one at a time
            var ret = new Dictionary<string, string>();
            fixed (byte* ptr = bytes)
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        return null;
                    }
                    var propertyName = reader.GetString()!;
                    // Read and skip and capture
                    if (!reader.Read())
                    {
                        return null;
                    }
                    var beginIndex = (int)reader.TokenStartIndex;
                    reader.Skip();
                    ret[propertyName] = ByteArrayRef.StrictUTF8.GetString(
                        ptr + beginIndex, (int)reader.BytesConsumed - beginIndex);
                }
            }
            return ret;
        }

        /// <inheritdoc />
        protected override unsafe bool ReleaseHandle()
        {
            forwardLogger = null;
            forwardLoggerCallback?.Free();
            Interop.Methods.temporal_core_runtime_free(Ptr);
            return true;
        }

        private static string LogMessageFormatter(ForwardedLog state, Exception? error) =>
            state.ToString();

        private unsafe void OnLog(Interop.TemporalCoreForwardedLogLevel coreLevel, Interop.TemporalCoreForwardedLog* coreLog)
        {
            if (forwardLogger is not { } logger)
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
            IReadOnlyDictionary<string, string>? jsonFields = null;
            if (forwardLoggerIncludeFields)
            {
                try
                {
                    var fieldBytes = Interop.Methods.temporal_core_forwarded_log_fields_json(coreLog);
                    jsonFields = ReadJsonObjectToRawValues(new(fieldBytes.data, (int)fieldBytes.size));
                }
#pragma warning disable CA1031 // We are ok swallowing all exceptions
                catch
                {
                }
#pragma warning restore CA1031
            }
            var log = new ForwardedLog(
                Level: level,
                Target: ByteArrayRef.ToUtf8(Interop.Methods.temporal_core_forwarded_log_target(coreLog)),
                Message: ByteArrayRef.ToUtf8(Interop.Methods.temporal_core_forwarded_log_message(coreLog)),
                TimestampMilliseconds: Interop.Methods.temporal_core_forwarded_log_timestamp_millis(coreLog),
                JsonFields: jsonFields);
            logger.Log(level, 0, log, null, ForwardLogMessageFormatter);
        }
    }
}
