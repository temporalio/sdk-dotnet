using System;
using System.Net;
using System.Net.Sockets;

namespace Temporalio.Extensions.Aws.SecretsManager
{
    /// <summary>
    /// Fetcher selection.
    /// </summary>
    internal static class Fetchers
    {
        /// <summary>
        /// Env var for the Lambda extension's port.
        /// </summary>
        public const string LambdaExtensionPortEnvVar = "PARAMETERS_SECRETS_EXTENSION_HTTP_PORT";

        /// <summary>
        /// Host of the Lambda extension.
        /// </summary>
        public const string LambdaExtensionHost = "localhost";

        /// <summary>
        /// Bounds each request made by the fetchers this package creates.
        /// </summary>
        public static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Returns a fetcher for the Lambda extension if it is detected, and otherwise an SDK
        /// fetcher with a client created from the default AWS config.
        /// </summary>
        /// <remarks>
        /// The extension doesn't set PARAMETERS_SECRETS_EXTENSION_HTTP_PORT in the function's
        /// environment unless the user configured it, so if it is unset and running in Lambda, the
        /// extension's default port is probed. Setting it to empty disables detection, for when
        /// another service uses the default port.
        /// </remarks>
        /// <param name="getEnv">Reads process environment variables.</param>
        /// <returns>The fetcher.</returns>
        public static IFetcher NewDefaultFetcher(Func<string, string?> getEnv)
        {
            const string LambdaFunctionNameEnvVar = "AWS_LAMBDA_FUNCTION_NAME";
            const int DefaultLambdaExtensionPort = 2773;
            var port = getEnv(LambdaExtensionPortEnvVar);
            if (port == null)
            {
                // Only try to auto-detect the Lambda extension if we're in a Lambda function
                if (string.IsNullOrEmpty(getEnv(LambdaFunctionNameEnvVar)) ||
                    !Probe(DefaultLambdaExtensionPort))
                {
                    return new SdkFetcher(null);
                }
                port = DefaultLambdaExtensionPort.ToString();
            }
            if (port.Length == 0)
            {
                return new SdkFetcher(null);
            }
            return new LambdaExtensionFetcher(port, getEnv);
        }

        /// <summary>
        /// Whether something accepts connections on the given local port.
        /// </summary>
        /// <param name="port">Port.</param>
        /// <returns>Whether something is listening.</returns>
        public static bool Probe(int port)
        {
            // Connects synchronously on this thread, since an async connect completes on the
            // thread pool, which may be too busy to run it in time, e.g. at startup. A local
            // connect is either accepted or refused almost immediately, so the timeout only bounds
            // pathological cases. The loopback addresses are used directly to skip resolving
            // localhost.
            foreach (var address in new[] { IPAddress.Loopback, IPAddress.IPv6Loopback })
            {
                try
                {
                    using var socket = new Socket(
                        address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    socket.Blocking = false;
                    try
                    {
                        socket.Connect(address, port);
                        return true;
                    }
                    catch (SocketException e) when (
                        e.SocketErrorCode is SocketError.WouldBlock or SocketError.InProgress)
                    {
                    }
                    // Writable once the connect completes, including when it fails on some
                    // platforms, so check its error too
                    if (socket.Poll(1_000_000, SelectMode.SelectWrite) &&
                        (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error)! == 0)
                    {
                        return true;
                    }
                }
                catch (SocketException)
                {
                    // Refused, or the address family isn't supported
                }
            }
            return false;
        }
    }
}
