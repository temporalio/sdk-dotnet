using System;

namespace Temporalio.Bridge
{
    internal static class OptionsExtensions
    {
        private static readonly ByteArrayRef ClientName = ByteArrayRef.FromUTF8("temporal-dotnet");
        private static readonly ByteArrayRef ClientVersion = ByteArrayRef.FromUTF8(
            Temporalio.Runtime.Version
        );
        private static readonly ByteArrayRef SdkName = ByteArrayRef.FromUTF8("sdk-dotnet");

        public static unsafe Interop.ClientOptions ToInteropOptions(
            this Temporalio.Client.TemporalConnectionOptions options,
            Scope scope
        )
        {
            if (options.TargetHost == null)
            {
                throw new ArgumentException("TargetHost is required");
            }
            else if (options.TargetHost.Contains("://"))
            {
                throw new ArgumentException("TargetHost cannot have ://");
            }
            return new Interop.ClientOptions()
            {
                target_url = scope.ByteArray($"http://{options.TargetHost}"),
                client_name = ClientName.Ref,
                client_version = ClientVersion.Ref,
                metadata = scope.Metadata(options.RpcMetadata),
                identity = scope.ByteArray(
                    options.Identity
                        ?? System.Diagnostics.Process.GetCurrentProcess().Id
                            + "@"
                            + System.Net.Dns.GetHostName()
                ),
                tls_options =
                    options.TlsOptions == null
                        ? null
                        : scope.Pointer(options.TlsOptions.ToInteropOptions(scope)),
                retry_options =
                    options.RpcRetryOptions == null
                        ? null
                        : scope.Pointer(options.RpcRetryOptions.ToInteropOptions())
            };
        }

        public static Interop.ClientTlsOptions ToInteropOptions(
            this Temporalio.Client.TlsOptions options,
            Scope scope
        )
        {
            var hasClientCert = options.ClientCert != null && options.ClientCert.Length > 0;
            var hasClientKey =
                options.ClientPrivateKey != null && options.ClientPrivateKey.Length > 0;
            if (hasClientCert != hasClientKey)
            {
                throw new ArgumentException(
                    "Client cert and private key must both be present or neither"
                );
            }
            return new Interop.ClientTlsOptions()
            {
                server_root_ca_cert = scope.ByteArray(options.ServerRootCACert),
                domain = scope.ByteArray(options.Domain),
                client_cert = scope.ByteArray(options.ClientCert),
                client_private_key = scope.ByteArray(options.ClientPrivateKey)
            };
        }

        public static Interop.ClientRetryOptions ToInteropOptions(
            this Temporalio.Client.RpcRetryOptions options
        )
        {
            return new Interop.ClientRetryOptions()
            {
                initial_interval_millis = (ulong)options.InitialInterval.TotalMilliseconds,
                randomization_factor = options.RandomizationFactor,
                multiplier = options.Multiplier,
                max_interval_millis = (ulong)options.MaxInterval.TotalMilliseconds,
                max_elapsed_time_millis = (ulong)(
                    options.MaxElapsedTime == null
                        ? 0
                        : options.MaxElapsedTime.Value.TotalMilliseconds
                ),
                max_retries = (UIntPtr)options.MaxRetries
            };
        }

        public static unsafe Interop.TemporaliteOptions ToInteropOptions(
            this Testing.WorkflowEnvironmentStartLocalOptions options,
            Scope scope
        )
        {
            // Use TargetHost to get IP + Port
            options.ParseTargetHost(out string? ip, out int? port);
            ip ??= "127.0.0.1";
            return new Interop.TemporaliteOptions()
            {
                test_server = scope.Pointer(
                    new Interop.TestServerOptions()
                    {
                        existing_path = scope.ByteArray(options.TemporaliteOptions.ExistingPath),
                        sdk_name = SdkName.Ref,
                        sdk_version = ClientVersion.Ref,
                        download_version = scope.ByteArray(
                            options.TemporaliteOptions.DownloadVersion
                        ),
                        download_dest_dir = scope.ByteArray(options.DownloadDirectory),
                        port = (ushort)(port ?? 0),
                        extra_args = scope.NewlineDelimited(options.TemporaliteOptions.ExtraArgs)
                    }
                ),
                namespace_ = scope.ByteArray(options.Namespace),
                ip = scope.ByteArray(ip),
                database_filename = scope.ByteArray(options.TemporaliteOptions.DatabaseFilename),
                ui = (byte)(options.UI ? 1 : 0),
                log_format = scope.ByteArray(options.TemporaliteOptions.LogFormat),
                log_level = scope.ByteArray(options.TemporaliteOptions.LogLevel)
            };
        }

        public static Interop.TestServerOptions ToInteropOptions(
            this Testing.WorkflowEnvironmentStartTimeSkippingOptions options,
            Scope scope
        )
        {
            // Use TargetHost to get IP + Port
            options.ParseTargetHost(out string? ip, out int? port);
            if (ip != "" && ip != "127.0.0.1" && ip != "localhost")
            {
                throw new InvalidOperationException(
                    "TargetHost can only specify empty, localhost, or 127.0.0.1 host"
                );
            }
            return new Interop.TestServerOptions()
            {
                existing_path = scope.ByteArray(options.TestServerOptions.ExistingPath),
                sdk_name = SdkName.Ref,
                sdk_version = ClientVersion.Ref,
                download_version = scope.ByteArray(options.TestServerOptions.DownloadVersion),
                download_dest_dir = scope.ByteArray(options.DownloadDirectory),
                port = (ushort)(port ?? 0),
                extra_args = scope.NewlineDelimited(options.TestServerOptions.ExtraArgs)
            };
        }
    }
}
