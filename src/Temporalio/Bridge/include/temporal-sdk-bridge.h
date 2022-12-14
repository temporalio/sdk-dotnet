#pragma once

#include <stdarg.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdlib.h>

typedef enum RpcService {
  Workflow = 1,
  Operator,
  Test,
  Health,
} RpcService;

typedef struct CancellationToken CancellationToken;

typedef struct Client Client;

typedef struct EphemeralServer EphemeralServer;

typedef struct Runtime Runtime;

typedef struct ByteArrayRef {
  const uint8_t *data;
  size_t size;
} ByteArrayRef;

typedef struct ClientTlsOptions {
  struct ByteArrayRef server_root_ca_cert;
  struct ByteArrayRef domain;
  struct ByteArrayRef client_cert;
  struct ByteArrayRef client_private_key;
} ClientTlsOptions;

typedef struct ClientRetryOptions {
  uint64_t initial_interval_millis;
  double randomization_factor;
  double multiplier;
  uint64_t max_interval_millis;
  uint64_t max_elapsed_time_millis;
  uintptr_t max_retries;
} ClientRetryOptions;

typedef struct ClientOptions {
  struct ByteArrayRef target_url;
  struct ByteArrayRef client_name;
  struct ByteArrayRef client_version;
  /**
   * Metadata is <key1>\n<value1>\n<key2>\n<value2>. Metadata keys or
   * values cannot contain a newline within.
   */
  struct ByteArrayRef metadata;
  struct ByteArrayRef identity;
  const struct ClientTlsOptions *tls_options;
  const struct ClientRetryOptions *retry_options;
} ClientOptions;

typedef struct ByteArray {
  const uint8_t *data;
  size_t size;
  /**
   * For internal use only.
   */
  size_t cap;
  /**
   * For internal use only.
   */
  bool disable_free;
} ByteArray;

/**
 * If success or fail are not null, they must be manually freed when done.
 */
typedef void (*ClientConnectCallback)(void *user_data, struct Client *success, const struct ByteArray *fail);

typedef struct RpcCallOptions {
  enum RpcService service;
  struct ByteArrayRef rpc;
  struct ByteArrayRef req;
  bool retry;
  /**
   * Metadata is <key1>\n<value1>\n<key2>\n<value2>. Metadata keys or
   * values cannot contain a newline within.
   */
  struct ByteArrayRef metadata;
  /**
   * 0 means no timeout
   */
  uint32_t timeout_millis;
  const struct CancellationToken *cancellation_token;
} RpcCallOptions;

/**
 * If success or fail are not null, they must be manually freed when done.
 */
typedef void (*ClientRpcCallCallback)(void *user_data, const struct ByteArray *success, const struct ByteArray *fail);

/**
 * If success or fail are not null, they must be manually freed when done.
 * Runtime is always present, but it should never be used if fail is present,
 * only freed after fail is freed.
 */
typedef struct RuntimeOrFail {
  struct Runtime *runtime;
  const struct ByteArray *fail;
} RuntimeOrFail;

typedef struct OpenTelemetryOptions {
  struct ByteArrayRef url;
  /**
   * Headers are <key1>\n<value1>\n<key2>\n<value2>. Header keys or values
   * cannot contain a newline within.
   */
  struct ByteArrayRef headers;
  uint32_t metric_periodicity_millis;
} OpenTelemetryOptions;

typedef struct TracingOptions {
  struct ByteArrayRef filter;
  struct OpenTelemetryOptions opentelemetry;
} TracingOptions;

typedef struct LoggingOptions {
  struct ByteArrayRef filter;
  bool forward;
} LoggingOptions;

typedef struct PrometheusOptions {
  struct ByteArrayRef bind_address;
} PrometheusOptions;

typedef struct MetricsOptions {
  const struct OpenTelemetryOptions *opentelemetry;
  const struct PrometheusOptions *prometheus;
} MetricsOptions;

typedef struct TelemetryOptions {
  const struct TracingOptions *tracing;
  const struct LoggingOptions *logging;
  const struct MetricsOptions *metrics;
} TelemetryOptions;

typedef struct RuntimeOptions {
  const struct TelemetryOptions *telemetry;
} RuntimeOptions;

typedef struct TestServerOptions {
  /**
   * Empty means default behavior
   */
  struct ByteArrayRef existing_path;
  struct ByteArrayRef sdk_name;
  struct ByteArrayRef sdk_version;
  struct ByteArrayRef download_version;
  /**
   * Empty means default behavior
   */
  struct ByteArrayRef download_dest_dir;
  /**
   * 0 means default behavior
   */
  uint16_t port;
  /**
   * Newline delimited
   */
  struct ByteArrayRef extra_args;
} TestServerOptions;

typedef struct TemporaliteOptions {
  /**
   * Must always be present
   */
  const struct TestServerOptions *test_server;
  struct ByteArrayRef namespace_;
  struct ByteArrayRef ip;
  /**
   * Empty means default behavior
   */
  struct ByteArrayRef database_filename;
  bool ui;
  struct ByteArrayRef log_format;
  struct ByteArrayRef log_level;
} TemporaliteOptions;

/**
 * Anything besides user data must be freed if non-null.
 */
typedef void (*EphemeralServerStartCallback)(void *user_data, struct EphemeralServer *success, const struct ByteArray *success_target, const struct ByteArray *fail);

typedef void (*EphemeralServerShutdownCallback)(void *user_data, const struct ByteArray *fail);

#ifdef __cplusplus
extern "C" {
#endif // __cplusplus

struct CancellationToken *cancellation_token_new(void);

void cancellation_token_cancel(struct CancellationToken *token);

void cancellation_token_free(struct CancellationToken *token);

/**
 * Runtime must live as long as client. Options and user data must live through
 * callback.
 */
void client_connect(struct Runtime *runtime,
                    const struct ClientOptions *options,
                    void *user_data,
                    ClientConnectCallback callback);

void client_free(struct Client *client);

/**
 * Client, options, and user data must live through callback.
 */
void client_rpc_call(struct Client *client,
                     const struct RpcCallOptions *options,
                     void *user_data,
                     ClientRpcCallCallback callback);

struct RuntimeOrFail runtime_new(const struct RuntimeOptions *options);

void runtime_free(struct Runtime *runtime);

void byte_array_free(struct Runtime *runtime, const struct ByteArray *bytes);

/**
 * Runtime must live as long as server. Options and user data must live through
 * callback.
 */
void ephemeral_server_start_temporalite(struct Runtime *runtime,
                                        const struct TemporaliteOptions *options,
                                        void *user_data,
                                        EphemeralServerStartCallback callback);

/**
 * Runtime must live as long as server. Options and user data must live through
 * callback.
 */
void ephemeral_server_start_test_server(struct Runtime *runtime,
                                        const struct TestServerOptions *options,
                                        void *user_data,
                                        EphemeralServerStartCallback callback);

void ephemeral_server_free(struct EphemeralServer *server);

void ephemeral_server_shutdown(struct EphemeralServer *server,
                               void *user_data,
                               EphemeralServerShutdownCallback callback);

#ifdef __cplusplus
} // extern "C"
#endif // __cplusplus
