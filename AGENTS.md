# Contributor Guidance for `sdk-dotnet`

This repository provides the Temporal .NET SDK for authoring Workflows, Activities, and Nexus
Operations. Use this
document as your quick reference when submitting pull requests.

## Requirements for coding agents

- Run tests with `dotnet test`. You do not need to run `dotnet build` separately first — `dotnet test`
  builds the projects it needs. To run a single test, use
  `dotnet test --filter "FullyQualifiedName=Temporalio.Tests.Client.TemporalClientTests.ConnectAsync_Connection_Succeeds"`.
  To see logs, add `--logger "console;verbosity=detailed"`.
- Many tests are integration tests that run against a Temporal Server. They use a real Temporal
  Server or the time-skipping test server that is lazily downloaded and run as a sub-process on first
  use (see `Temporalio.Testing.WorkflowEnvironment`). No separate server setup is required for most
  tests.
- The build treats warnings as errors (`TreatWarningsAsErrors`) and enables the full analyzer set
  (`AnalysisMode=AllEnabledByDefault`) plus StyleCop. A build that produces analyzer warnings will
  fail. Fix the underlying issue rather than suppressing it, unless a suppression is already the
  established pattern in `.editorconfig`.
- It is EXTREMELY IMPORTANT that any added comments should explain why something needs to be done,
  rather than what it is. Comments that simply state a fact easily understood from type signatures
  or other context should NEVER be added. Always prefer to avoid a comment unless it truly is
  clarifying something nonobvious.
- Always make every attempt to avoid explicit sleeps in test code. Instead rely on synchronization
  techniques (`WaitConditionAsync`, `TaskCompletionSource`, channels, etc.) or the time-skipping
  test environment.
- The SDK multi-targets several frameworks (.NET Framework >= 4.6.2, .NET Core >= 3.1, .NET Standard
  >= 2.0). Be mindful that newer BCL APIs may not exist on older targets; the `.editorconfig`
  disables several analyzers for this reason. Code must compile on all targets.
- Do not interrupt builds or tests unless they are taking more than 5 minutes. The first test run
  downloads the test server, and some Workflow/integration tests are slow; it is possible to
  introduce test hangs.
- Do not extract functions for relatively simple helpers that are only used in one location.

## Building and Testing

Building requires a recent .NET 10 SDK, Rust (`cargo` on the `PATH`), and the Protobuf compiler
(`protoc` on the `PATH`), since the native bridge is built from the `sdk-core` Rust submodule. Clone
the repository recursively so the submodule is present.

The following are enforced for each pull request (see `README.md`):

```bash
dotnet build                       # build all projects (also builds the Rust bridge DLL)
dotnet format --verify-no-changes  # ensure code is formatted (StyleCop + .editorconfig rules)
dotnet test                        # run unit and integration tests
dotnet pack -c Debug               # validate the public API surface (runs in the Pack target)
```

To run the tests as an in-proc program (helpful for debugging native pieces and seeing full
stdout/stderr):

```bash
dotnet run --project tests/Temporalio.Tests          # all tests
dotnet run --project tests/Temporalio.Tests -- --help  # see runner options
```

API documentation is generated with [docfx](https://dotnet.github.io/docfx/) via
`docfx src/Temporalio.ApiDoc/docfx.json`.

The following environment variables override the test environment to run against an external server:

- `TEMPORAL_TEST_CLIENT_TARGET_HOST` – must be set for any of the variables below to apply
- `TEMPORAL_TEST_CLIENT_NAMESPACE` – required if the above is set
- `TEMPORAL_TEST_CLIENT_CERT` / `TEMPORAL_TEST_CLIENT_KEY` – optional mTLS cert/key (both or neither)

## Expectations for Pull Requests

- Format and build (with analyzers clean) before submitting.
- Ensure all tests pass locally.
- Add a high-level, user-facing entry to the `## [Unreleased]` section of `CHANGELOG.md` for any
  user-facing change (new feature, behavior change, deprecation, breaking change, notable bug fix,
  or security fix). Internal-only changes (refactors, tests, CI, docs) do not need an entry. See
  `CONTRIBUTING.md`.
- Keep commit messages short and in the imperative mood.
- Provide a clear PR description outlining what changed and why.
- Reviewers expect new features or fixes to include corresponding tests when applicable.
- Public API changes are validated against the previously published package via
  `EnablePackageValidation`, which runs during `dotnet pack`. Most additions (new types, or new
  members on a class) are compatible and need no action, but some additions are breaking — e.g.
  adding a member to an existing public interface or an abstract member to a class breaks
  implementers. Any breaking change (a removed or altered surface, or one of these breaking
  additions) will fail validation; regenerate the baseline suppressions rather than hand-editing
  them:

  ```bash
  dotnet pack -c Debug /p:GenerateCompatibilitySuppressionFile=true
  ```

  Then review the diff to `src/Temporalio/CompatibilitySuppressions.xml` and keep only the intended
  entries — this repo sets `ApiCompatPermitUnnecessarySuppressions=false`, so stale suppressions are
  themselves an error. New public APIs require documentation comments (`GenerateDocumentationFile`
  is on).

## Review Checklist

Reviewers will look for:

- All builds, tests, lints, and formatting passing in CI.
    - Note that some tests cause intentional exceptions/panics. That does not mean the test failed.
      Only consider tests reported as failed by the harness to be a real problem.
- New tests covering behavior changes.
- Clear and concise code following existing style (StyleCop + `.editorconfig`).
- Documentation comments and `CHANGELOG.md` updates for any public API changes.

## Where Things Are

- `src/` – libraries and tooling projects.
  - `src/Temporalio/` – the main SDK. Notable areas:
    - `Client/` – clients for communicating with Temporal clusters
    - `Worker/` – the Worker that runs Workflows, Activities, and Nexus Operations
    - `Workflows/` – Workflow authoring API (attributes, `Workflow` static, determinism support)
    - `Activities/` – Activity authoring API and execution context
    - `Converters/` – data/payload conversion
    - `Nexus/` – Nexus Operation support
    - `Runtime/` – runtime, telemetry, and logging
    - `Testing/` – `WorkflowEnvironment` test server support
    - `Exceptions/` – Temporal exception types
    - `Api/` – generated protobuf types (do not edit by hand; see "Regenerating protos")
    - `Bridge/` – C# interop layer over the Rust core; `Bridge/sdk-core` is the Rust submodule
  - `src/Temporalio.Api.Generator/` – tool that regenerates the `Temporalio.Api.*` proto types
  - `src/Temporalio.ApiDoc/` – docfx config for API docs
  - `src/Temporalio.Extensions.DiagnosticSource/` – `System.Diagnostics.Metrics` support
  - `src/Temporalio.Extensions.Hosting/` – dependency injection and generic-host Worker support
  - `src/Temporalio.Extensions.OpenTelemetry/` – OpenTelemetry tracing support
- `tests/` – test projects.
  - `tests/Temporalio.Tests/` – the main test suite (unit + integration)
  - `tests/Temporalio.SimpleBench/`, `tests/Temporalio.SmokeTest*/` – benchmarks and smoke tests
- `Directory.Build.props` – shared MSBuild properties
- `Directory.Packages.props` – central package versions (this repo uses central package management).
- `.editorconfig` – analyzer/StyleCop rule configuration and Temporal-specific overrides.
- `README.md`, `CONTRIBUTING.md` – contributor and development guide.
- `bin/`, `obj/`, `target/` – compiled output. You never need to look in here.

## Notes

- The native Rust DLL is built automatically when the project is built. `protoc` may need to be on
  the `PATH` for the Rust DLL to build.
- Workflow code must be deterministic. The README's "Workflow Logic Constraints" section (including
  ".NET Task Determinism") explains the rules and the Workflow-specific `.editorconfig` overrides —
  read it before changing Workflow internals.
- The interop layer is regenerated from the C header with ClangSharpPInvokeGenerator:
  `ClangSharpPInvokeGenerator @src/Temporalio/Bridge/GenerateInterop.rsp`.
- Protobuf types are regenerated with `dotnet run --project src/Temporalio.Api.Generator` (requires
  `protoc` on the `PATH`; use `protoc` 23.x for now). Regenerate and commit the output rather than
  hand-editing generated files under `src/Temporalio/Api/`.
- `src/Temporalio/Bridge/sdk-core` is a git submodule pointing at
  [`sdk-rust`](https://github.com/temporalio/sdk-rust); change it via the submodule, not by editing
  files in place.
