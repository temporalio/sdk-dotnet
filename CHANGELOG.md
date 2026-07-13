<!--
High-level release notes.
Loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

When your PR includes a user-facing change, add an entry below under the
appropriate heading (create the heading if it does not yet exist). Within
each heading content can be free-form. Feel free to include examples, links
to docs, or any other relevant information.

### Added            — new features
### Changed          — changes in existing functionality
### Deprecated       — soon-to-be-removed features
### Breaking Changes — removed or backwards-incompatible features
### Fixed            — notable bug fixes
### Security         — notable security fixes
-->

# Changelog

## [Unreleased]

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
