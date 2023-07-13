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

typedef struct Random Random;

typedef struct Runtime Runtime;

typedef struct Worker Worker;

typedef struct WorkerReplayPusher WorkerReplayPusher;

typedef struct ByteArrayRef {
  const uint8_t *data;
  size_t size;
} ByteArrayRef;

/**
 * Metadata is <key1>\n<value1>\n<key2>\n<value2>. Metadata keys or
 * values cannot contain a newline within.
 */
typedef struct ByteArrayRef MetadataRef;

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
  MetadataRef metadata;
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
  MetadataRef metadata;
  /**
   * 0 means no timeout
   */
  uint32_t timeout_millis;
  const struct CancellationToken *cancellation_token;
} RpcCallOptions;

/**
 * If success or failure byte arrays inside fail are not null, they must be
 * manually freed when done. Either success or failure_message are always
 * present. Status code may still be 0 with a failure message. Failure details
 * represent a protobuf gRPC status message.
 */
typedef void (*ClientRpcCallCallback)(void *user_data, const struct ByteArray *success, uint32_t status_code, const struct ByteArray *failure_message, const struct ByteArray *failure_details);

/**
 * If fail is not null, it must be manually freed when done. Runtime is always
 * present, but it should never be used if fail is present, only freed after
 * fail is freed using it.
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

typedef struct DevServerOptions {
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
} DevServerOptions;

/**
 * Anything besides user data must be freed if non-null.
 */
typedef void (*EphemeralServerStartCallback)(void *user_data, struct EphemeralServer *success, const struct ByteArray *success_target, const struct ByteArray *fail);

typedef void (*EphemeralServerShutdownCallback)(void *user_data, const struct ByteArray *fail);

/**
 * Only runtime or fail will be non-null. Whichever is must be freed when done.
 */
typedef struct WorkerOrFail {
  struct Worker *worker;
  const struct ByteArray *fail;
} WorkerOrFail;

typedef struct WorkerOptions {
  struct ByteArrayRef namespace_;
  struct ByteArrayRef task_queue;
  struct ByteArrayRef build_id;
  struct ByteArrayRef identity_override;
  uint32_t max_cached_workflows;
  uint32_t max_outstanding_workflow_tasks;
  uint32_t max_outstanding_activities;
  uint32_t max_outstanding_local_activities;
  bool no_remote_activities;
  uint64_t sticky_queue_schedule_to_start_timeout_millis;
  uint64_t max_heartbeat_throttle_interval_millis;
  uint64_t default_heartbeat_throttle_interval_millis;
  double max_activities_per_second;
  double max_task_queue_activities_per_second;
  uint64_t graceful_shutdown_period_millis;
  bool use_worker_versioning;
} WorkerOptions;

/**
 * If success or fail are present, they must be freed. They will both be null
 * if this is a result of a poll shutdown.
 */
typedef void (*WorkerPollCallback)(void *user_data, const struct ByteArray *success, const struct ByteArray *fail);

/**
 * If fail is present, it must be freed.
 */
typedef void (*WorkerCallback)(void *user_data, const struct ByteArray *fail);

typedef struct WorkerReplayerOrFail {
  struct Worker *worker;
  struct WorkerReplayPusher *worker_replay_pusher;
  const struct ByteArray *fail;
} WorkerReplayerOrFail;

typedef struct WorkerReplayPushResult {
  const struct ByteArray *fail;
} WorkerReplayPushResult;

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

void client_update_metadata(struct Client *client, struct ByteArrayRef metadata);

/**
 * Client, options, and user data must live through callback.
 */
void client_rpc_call(struct Client *client,
                     const struct RpcCallOptions *options,
                     void *user_data,
                     ClientRpcCallCallback callback);

struct Random *random_new(uint64_t seed);

void random_free(struct Random *random);

int32_t random_int32_range(struct Random *random, int32_t min, int32_t max, bool max_inclusive);

double random_double_range(struct Random *random, double min, double max, bool max_inclusive);

void random_fill_bytes(struct Random *random, struct ByteArrayRef bytes);

struct RuntimeOrFail runtime_new(const struct RuntimeOptions *options);

void runtime_free(struct Runtime *runtime);

void byte_array_free(struct Runtime *runtime, const struct ByteArray *bytes);

/**
 * Runtime must live as long as server. Options and user data must live through
 * callback.
 */
void ephemeral_server_start_dev_server(struct Runtime *runtime,
                                       const struct DevServerOptions *options,
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

struct WorkerOrFail worker_new(struct Client *client, const struct WorkerOptions *options);

void worker_free(struct Worker *worker);

void worker_poll_workflow_activation(struct Worker *worker,
                                     void *user_data,
                                     WorkerPollCallback callback);

void worker_poll_activity_task(struct Worker *worker, void *user_data, WorkerPollCallback callback);

void worker_complete_workflow_activation(struct Worker *worker,
                                         struct ByteArrayRef completion,
                                         void *user_data,
                                         WorkerCallback callback);

void worker_complete_activity_task(struct Worker *worker,
                                   struct ByteArrayRef completion,
                                   void *user_data,
                                   WorkerCallback callback);

/**
 * Returns error if any. Must be freed if returned.
 */
const struct ByteArray *worker_record_activity_heartbeat(struct Worker *worker,
                                                         struct ByteArrayRef heartbeat);

void worker_request_workflow_eviction(struct Worker *worker, struct ByteArrayRef run_id);

void worker_initiate_shutdown(struct Worker *worker);

void worker_finalize_shutdown(struct Worker *worker, void *user_data, WorkerCallback callback);

struct WorkerReplayerOrFail worker_replayer_new(struct Runtime *runtime,
                                                const struct WorkerOptions *options);

void worker_replay_pusher_free(struct WorkerReplayPusher *worker_replay_pusher);

struct WorkerReplayPushResult worker_replay_push(struct Worker *worker,
                                                 struct WorkerReplayPusher *worker_replay_pusher,
                                                 struct ByteArrayRef workflow_id,
                                                 struct ByteArrayRef history);

#ifdef __cplusplus
} // extern "C"
#endif // __cplusplus
