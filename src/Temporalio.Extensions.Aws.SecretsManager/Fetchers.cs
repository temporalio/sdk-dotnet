using System;
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
                    !Probe(LambdaExtensionHost, DefaultLambdaExtensionPort))
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
        /// Whether something accepts connections on the given port.
        /// </summary>
        /// <param name="host">Host.</param>
        /// <param name="port">Port.</param>
        /// <returns>Whether something is listening.</returns>
        public static bool Probe(string host, int port)
        {
            // Probing a local port is either accepted or refused almost immediately, so the
            // timeout only bounds pathological cases
            try
            {
                using var client = new TcpClient();
                var connect = client.ConnectAsync(host, port);
#pragma warning disable VSTHRD002 // Waiting for a local connect with a short timeout
                return connect.Wait(TimeSpan.FromMilliseconds(100)) && client.Connected;
#pragma warning restore VSTHRD002
            }
            catch (AggregateException)
            {
                return false;
            }
            catch (SocketException)
            {
                return false;
            }
        }
    }
}
