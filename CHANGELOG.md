<!--
High-level release notes.
Loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

When your PR includes a user-facing change, add an entry below under the
appropriate heading (create the heading if it does not yet exist). Within
each heading content can be free-form. Feel free to include examples, links
to docs, or any other relevant information.

### Added                   — new features
### Changed                 — changes in existing functionality
### Deprecated              — soon-to-be-removed features
### :boom: Breaking Changes — removed or backwards-incompatible features
### Fixed                   — notable bug fixes
### Security                — notable security fixes
-->

# Changelog

## [Unreleased]

### :boom: Breaking Changes

- Removed the experimental `SignalWithStartWorkflowOptions.RequestId`. Request IDs for
  workflow-side signal-with-start are now assigned internally and are no longer user-settable.

### Added

- Added `PayloadValidationError.CreateException`, which payload converters and codecs can use to
  report invalid Nexus operation input with structured details.
- The `temporal_activity_execution_failed` and `temporal_local_activity_execution_failed` worker
  metrics now carry a `failure_reason` attribute. Each metric is now split into one time series per
  reason, which may affect existing dashboards.
- Workflow task completions larger than the gRPC request size limit are now paginated
  automatically when the namespace supports it. Paginated workflow task completions require
  Temporal Server 1.32.0 or later.

### Changed

- A non-retryable `ApplicationFailureException` with error type `PayloadValidationError` thrown by a
  payload codec or payload converter while decoding Nexus operation input is now reported as a
  non-retryable `BadRequest` handler exception (with the application failure as its cause) instead of
  an `Internal` one. This lets a data converter signal that the input itself is invalid. The handler
  exception is reported as `Invalid operation input`, which is distinct from the `failed to decode
  Nexus operation input` message used when decoding itself fails. Application failures of any other
  error type, and retryable `PayloadValidationError` failures, keep their existing behavior.

### Fixed

- Worker shutdown now drains activity completions that are still flushing their result to the
  server before finishing. Previously such a completion — typically one whose final heartbeat RPC
  was still in flight — could be permanently stranded by shutdown, so the activity's result was
  never reported and the server had to time the attempt out before retrying it.
- Workers with a small workflow cache no longer briefly stop accepting new workflows. Sticky
  workflow-task pollers could consume every workflow-cache permit and starve the non-sticky poller,
  so the worker would stop picking up new workflows until a poll timed out (up to ~60s).
- Nexus tasks are now timed out locally even when the server sends a `request-timeout` header that
  falls outside the Nexus duration grammar, such as a negative value for a task whose deadline has
  already elapsed, a sub-millisecond unit, or a multi-unit value like `1m30s`. Previously such a
  header was ignored entirely, so the handler was never told the task had timed out, and a task
  left unanswered could block worker shutdown indefinitely.
- Update-with-start calls now use the long-poll timeout instead of the normal RPC timeout, avoiding
  premature failures while waiting for an update to reach its requested stage.
- An activity failure caused by oversized final heartbeat details is now counted in the
  `temporal_activity_execution_failed` metric as `failure_reason="PayloadsTooLarge"`. Previously it
  was counted under the reason for the failure the activity itself reported, and was not counted at
  all when that failure was benign, even though a payload-limit failure was reported instead.
- Setting `PrometheusOptions.HasCounterTotalSuffix` now actually appends `_total` to counter metric
  names in the Prometheus exporter output.
- Workers now warn when autoscaling task polling encounters errors continuously for one minute.
  Repeated warnings use exponential backoff up to 15-minute intervals and stop after polling
  recovers.
- Workers no longer send worker heartbeats or appear in centralized heartbeat reports before they
  begin polling.
- Ephemeral server processes (such as those started by `WorkflowEnvironment.StartLocalAsync`) no
  longer leak when the server fails to start.

## [1.18.0] - 2026-08-13

### :boom: Breaking Changes

- Removed the unused `NexusHandlerFailureException`; Nexus handler failures are represented by
  `NexusRpc.Handlers.HandlerException`. The SDK-created `TemporalNexusClient` implementation is now
  internal; Nexus operation handlers continue to receive its public `ITemporalNexusClient`
  interface.
- By default, workers now proactively validate outbound payload/memo sizes before sending: a field
  over the warn threshold is logged (`[TMPRL1103]` at
  `WARN`) but still sent, while a task completion over the error limit is failed retryably
  (`[TMPRL1103]` at `ERROR`) instead of sent. Previously these reached the server, which terminated
  the workflow or failed the activity non-retryably; failing retryably instead lets a corrected
  workflow or activity be redeployed and recover. Tune warn thresholds via
  `TemporalConnectionOptions.PayloadLimits`. If you use a proxy between the worker and server that
  alters the size of payloads (e.g. compression, encryption, external storage), it is advised that
  you disable size enforcement by setting `DisablePayloadErrorLimit` to `true` on the worker.
- Activity failures now include the latest heartbeat details atomically instead of force-flushing a
  throttled heartbeat first. Temporal Server 1.16.0 or newer is required to guarantee those details
  are preserved on failure; workers warn when the server does not advertise support.

### Added

- Added the experimental `Temporalio.Extensions.Gcp.CloudRun.OpenTelemetry` package, with
  OpenTelemetry helpers (metrics + tracing over OTLP to a collector sidecar) for Temporal workers
  running on Google Cloud Run. The Cloud Run and existing AWS Lambda helpers now share a
  provider-neutral configuration layer for OTLP tracing, Temporal metrics and interceptors, and
  shutdown flushing while retaining provider-specific defaults and lifecycle behavior.
- Added experimental SDK payload converter support for values and target types that expose
  Temporal transfer type conversion hooks. This lets hook-aware types delegate
  their wire representation to the configured payload converter, preserving SDK
  behavior such as serialization contexts.
- Added `TemporalWorkerOptions.MaxEagerActivityReservationsPerWorkflowTask` to configure the
  maximum number of activity slots reserved for eager execution per workflow task. Configured
  values must be positive; use `DisableEagerActivityExecution` to disable eager execution.
- Added `WorkflowEnvironment.CreateFromEnvConfigAsync` for creating test workflow environments from
  client environment configuration.
- Added the experimental `TemporalWorkerOptions.PatchActivationCallback`, allowing workers to
  decide whether a first non-replay `Workflow.Patched` call should activate a patch during rolling
  deployments.
- Support standalone activities as Nexus operations. `ITemporalNexusClient.StartActivityAsync`
  backs a Nexus operation with a standalone activity (async only). Cancellation of
  activity-execution operations can be customized by overriding
  `TemporalOperationHandler<TInput, TResult>.CancelActivityExecutionAsync`.
- Worker heartbeats now report the hosting .NET runtime and its version. Set the new
  `TemporalRuntimeOptions.DisableEnvironmentInfo` to omit all runtime, hosting, and platform
  information from heartbeats.
- Workers are now automatically enrolled into poller autoscaling when the namespace advertises the
  `poller_autoscaling_auto_enroll` capability. This only applies to poller types left at their
  default (the worker set neither a fixed poller count nor a poller behavior); explicitly configured
  pollers are left unchanged.
- Workers now log a [TMPRL1104] warning when a workflow task takes longer than 5 seconds. Set
  `TEMPORAL_WORKFLOW_TASK_DURATION_WARN_SECONDS` to change the threshold.

### Changed

- Nexus support for calling operations from workflows and handling workflow-backed operations with
  `WorkflowRunOperationHandler` is now generally available (GA). Standalone Nexus Operation and `TemporalOperationHandler`,
  including workflow updates and standalone activities as Nexus operations, remains experimental.
- User metadata fields (StaticSummary, StaticDetails, CurrentDetails, Activity Summary, Timer
  Summary) are no longer marked as experimental.
- Hardened read-only workflow context enforcement so queries, update validators, and patch activation
  callbacks cannot mutate handlers or workflow details, invoke patches, or schedule workflow work.
  Patch activation callbacks also cannot use workflow randomness or issue workflow commands.
- A `NexusRpc.Handlers.HandlerException` thrown by a payload codec or payload converter while
  decoding Nexus operation input is now propagated as-is instead of being wrapped in an `Internal`
  (codec) or `BadRequest` (converter) handler exception. This matches the existing pass-through
  behavior for `ApplicationFailureException` and lets codecs and converters control the resulting
  Nexus error type and retry behavior.

### Fixed

- Workers no longer advertise a worker control task queue unless the namespace supports worker
  heartbeats and commands and the built-in Nexus command worker is running.
- Local activity resolutions are now delivered to workflows as each activity completes instead of
  waiting for every local activity in the workflow task. This allows sequences of short local
  activities to make progress while a long-running local activity executes in parallel, while
  preserving the resolution ordering recorded in existing histories during replay.
- Try-cancel child workflows no longer cause nondeterminism when they complete or fail after their
  cancellation was requested.

### [1.17.0] - 2026-07-13

### Added

- Added experimental AWS Lambda worker support packages, including OpenTelemetry helpers for Lambda workers.
- Added experimental workflow-side `Workflow.SignalWithStartWorkflowAsync` support.

### Changed

- Changed the default `TemporalConnectionOptions.GrpcCompression` to `GrpcCompression.Gzip`, so
  connections now compress outbound requests and accept gzip-compressed responses by default. If the remote
  service does not support gzip compression, the connection is downgraded to uncompressed requests. Set it
  to `GrpcCompression.None` to opt out.

### Fixed

- Fixed `ClientEnvConfig` empty `OverrideEnvVars` handling so an explicit empty dictionary no
  longer falls back to process environment variables.
- Fixed `ClientEnvConfig` TLS-disabled profiles to preserve disabled TLS in connection options.
- OTLP metric export failures are now logged through Core telemetry when OpenTelemetry's periodic metric reader reports an export error.
- Worker heartbeat now samples host CPU/memory at the heartbeat interval (only when enabled) rather than every 100ms.
### Added

- Added the `[TemporalOperation]` attribute for declaring a Temporal-backed Nexus operation start
  handler directly on a method within a `[NexusServiceHandler]` class. It is mutually
  exclusive with `[NexusOperationHandler]` on the same method.

### [1.16.0] - 2026-07-01

### Added

- Added `TemporalConnectionOptions.GrpcCompression` to control transport-level gRPC compression for
  all calls made over the connection. Use `GrpcCompression.Gzip` to compress or `GrpcCompression.None`
  to opt out. The default is `GrpcCompression.None`.
- Nexus operation link propagation. When a Nexus operation handler issues an outbound RPC (signal,
  signal-with-start, or starting a workflow), the inbound Nexus request links are now forwarded onto
  the target workflow so its history events link back to the caller, and the link the server returns
  for that event is attached to the caller workflow's Nexus operation history event. This makes the
  caller and callee mutually navigable in the UI for both workflow-based and standalone Nexus
  operations.
- Exposed `BackoffStartInterval` for continue-as-new, to allow the new workflow to start after a delay.

### Changed

- Reduced CPU usage of type-safe calls (e.g. `ExecuteChildWorkflowAsync(wf => wf.RunAsync(arg))`)
  by evaluating non-constant arguments via expression interpretation instead of full IL compilation, when supported by the runtime.
- Support workflow updates as Nexus operations. `ITemporalNexusClient.StartWorkflowUpdateAsync`
  backs a Nexus operation with a workflow update (async only, `WorkflowUpdateStage.Accepted`).
  Cancellation of update-workflow operations can be customized by overriding
  `TemporalOperationHandler<TInput, TResult>.CancelWorkflowUpdateAsync`.
