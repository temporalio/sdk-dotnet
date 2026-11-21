using System;
using System.Collections.Generic;
using Amazon.Runtime;
using Microsoft.Extensions.Logging;
using Temporalio.Common;

namespace Temporalio.Extensions.Aws.SecretsManager
{
    /// <summary>
    /// Metrics and logging for a client's runtime. Metric and label names must be the same across
    /// SDKs.
    /// </summary>
    internal class Telemetry
    {
        /// <summary>Metric or label name or value, the same across SDKs.</summary>
        public const string MetricFetchFailed = "temporal_aws_secrets_manager_fetch_failed";

        /// <summary>Metric or label name or value, the same across SDKs.</summary>
        public const string MetricFetchLatency = "temporal_aws_secrets_manager_fetch_latency";

        /// <summary>Metric or label name or value, the same across SDKs.</summary>
        public const string MetricCacheLookup = "temporal_aws_secrets_manager_cache_lookup";

        /// <summary>Metric or label name or value, the same across SDKs.</summary>
        public const string LabelSecretType = "secret_type";

        /// <summary>Metric or label name or value, the same across SDKs.</summary>
        public const string LabelFetchPath = "fetch_path";

        /// <summary>Metric or label name or value, the same across SDKs.</summary>
        public const string LabelFailureReason = "failure_reason";

        /// <summary>Metric or label name or value, the same across SDKs.</summary>
        public const string LabelFallback = "fallback";

        /// <summary>Metric or label name or value, the same across SDKs.</summary>
        public const string LabelOutcome = "outcome";

        /// <summary>Metric or label name or value, the same across SDKs.</summary>
        public const string LabelResult = "result";

        // secret_type values. Client cert and key secrets are fetched together and reported as
        // tls_client_cert in metrics.

        /// <summary>Metric or label name or value, the same across SDKs.</summary>
        public const string SecretTypeApiKey = "api_key";

        /// <summary>Metric or label name or value, the same across SDKs.</summary>
        public const string SecretTypeTlsClientCert = "tls_client_cert";

        /// <summary>Metric or label name or value, the same across SDKs.</summary>
        public const string SecretTypeTlsClientKey = "tls_client_key";

        /// <summary>Metric or label name or value, the same across SDKs.</summary>
        public const string SecretTypeTlsServerCACert = "tls_server_ca_cert";

        /// <summary>Metric or label name or value, the same across SDKs.</summary>
        public const string FetchPathExtension = "extension";

        /// <summary>Metric or label name or value, the same across SDKs.</summary>
        public const string FetchPathSdk = "sdk";

        // AWS error codes mapped to failure_reason values. Other codes are "other".
        private static readonly Dictionary<string, string> AwsErrorCodeFailureReasons = new()
        {
            ["ResourceNotFoundException"] = "resource_not_found",
            ["AccessDeniedException"] = "access_denied",
            ["DecryptionFailure"] = "decryption_failure",
            ["InvalidRequestException"] = "invalid_request",
            ["InvalidParameterException"] = "invalid_request",
            ["ThrottlingException"] = "throttling",
            ["InternalServiceError"] = "internal_service_error",
        };

        private readonly MetricCounter<long>? fetchFailed;
        private readonly MetricHistogram<TimeSpan>? fetchLatency;
        private readonly MetricCounter<long>? cacheLookup;

        /// <summary>
        /// Initializes a new instance of the <see cref="Telemetry"/> class.
        /// </summary>
        /// <param name="meter">Metric meter, or null to not record metrics.</param>
        /// <param name="logger">Logger.</param>
        public Telemetry(MetricMeter? meter, ILogger logger)
        {
            Logger = logger;
            if (meter != null)
            {
                fetchFailed = meter.CreateCounter<long>(
                    MetricFetchFailed, description: "Failed fetches of AWS Secrets Manager secrets");
                fetchLatency = meter.CreateHistogram<TimeSpan>(
                    MetricFetchLatency, description: "Latency of AWS Secrets Manager secret fetches");
                cacheLookup = meter.CreateCounter<long>(
                    MetricCacheLookup,
                    description: "Lookups of cached AWS Secrets Manager secrets that returned a value");
            }
        }

        /// <summary>
        /// Gets the logger.
        /// </summary>
        public ILogger Logger { get; }

        /// <summary>
        /// Returns the failure_reason metric label for a fetch error.
        /// </summary>
        /// <param name="err">Fetch error.</param>
        /// <returns>The label.</returns>
        public static string ClassifyFailure(Exception err)
        {
            switch (err)
            {
                case InvalidResponseException:
                    return "invalid_response";
                case LambdaExtensionStatusException statusErr:
                    // The Lambda extension doesn't return the underlying AWS error, only an HTTP
                    // status with a generic message, so its errors are only classified by status
                    // class
                    return statusErr.Status switch
                    {
                        >= 400 and < 500 => "client_error",
                        >= 500 and < 600 => "server_error",
                        _ => "other",
                    };
                case FetchTimeoutException:
                    return "timeout";
                case AmazonServiceException awsErr:
                    return awsErr.ErrorCode != null &&
                        AwsErrorCodeFailureReasons.TryGetValue(awsErr.ErrorCode, out var reason) ?
                        reason : "other";
                default:
                    return "other";
            }
        }

        /// <summary>
        /// Record a cache lookup that returned a value.
        /// </summary>
        /// <param name="tags">Metric tags.</param>
        public virtual void RecordCacheLookup(IEnumerable<KeyValuePair<string, object>> tags) =>
            Record(() => cacheLookup?.Add(1, tags));

        /// <summary>
        /// Record a failed fetch.
        /// </summary>
        /// <param name="tags">Metric tags.</param>
        public virtual void RecordFetchFailed(IEnumerable<KeyValuePair<string, object>> tags) =>
            Record(() => fetchFailed?.Add(1, tags));

        /// <summary>
        /// Record a fetch's latency.
        /// </summary>
        /// <param name="latency">Latency.</param>
        /// <param name="tags">Metric tags.</param>
        public virtual void RecordFetchLatency(
            TimeSpan latency, IEnumerable<KeyValuePair<string, object>> tags) =>
            Record(() => fetchLatency?.Record(latency, tags));

        /// <summary>
        /// Record a metric or log, so that a failing metric meter or logger doesn't affect
        /// fetching or refreshing secrets.
        /// </summary>
        /// <param name="fn">Recording function.</param>
#pragma warning disable CA1031 // Any recording failure is only logged, if the logger works
        public void Record(Action fn)
        {
            try
            {
                fn();
            }
            catch (Exception e)
            {
                try
                {
                    Logger.LogWarning(e, "Failed recording AWS Secrets Manager telemetry");
                }
                catch (Exception)
                {
                    // The logger itself is failing, so there's nowhere to report this
                }
            }
        }
#pragma warning restore CA1031
    }
}
