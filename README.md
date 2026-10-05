![Temporal .NET SDK](https://raw.githubusercontent.com/temporalio/assets/main/files/w/dotnet.png)

[![NuGet](https://img.shields.io/nuget/vpre/temporalio.svg?style=for-the-badge)](https://www.nuget.org/packages/Temporalio)
[![MIT](https://img.shields.io/github/license/temporalio/sdk-dotnet.svg?style=for-the-badge)](LICENSE)

[Temporal](https://temporal.io/) is a distributed, scalable, durable, and highly available orchestration engine used to
execute asynchronous, long-running business logic in a scalable and resilient way.

This repository contains the Temporal .NET SDK, the framework for authoring workflows and activities using .NET
programming languages, along with its extension packages.

**To install and use the SDK, see the [`Temporalio` package README](src/Temporalio/README.md).** It covers the quick
start, clients, workers, workflows, activities, Nexus, and testing.

Also see:

* [Application Development Guide](https://docs.temporal.io/develop/dotnet/)
* [.NET Samples](https://github.com/temporalio/samples-dotnet)
* [API Documentation](https://dotnet.temporal.io/api)

---

<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->
**Contents**

- [Packages](#packages)
- [Contributing](#contributing)
- [Development](#development)
  - [Build](#build)
  - [Code formatting](#code-formatting)
    - [VisualStudio Code](#visualstudio-code)
  - [Testing](#testing)
  - [Regenerating code](#regenerating-code)
  - [Regenerating API docs](#regenerating-api-docs)

<!-- END doctoc generated TOC please keep comment here to allow auto update -->

## Packages

Each package has its own README next to its project, which is also its NuGet package page.

| Package | Description |
| ------- | ----------- |
| [Temporalio](src/Temporalio/README.md) | The core SDK: clients, workers, workflows, activities, and Nexus operations |
| [Temporalio.Extensions.Aws.Lambda](src/Temporalio.Extensions.Aws.Lambda/README.md) | Running a worker inside an AWS Lambda invocation |
| [Temporalio.Extensions.Aws.Lambda.OpenTelemetry](src/Temporalio.Extensions.Aws.Lambda.OpenTelemetry/README.md) | OpenTelemetry helpers for workers running in AWS Lambda |
| [Temporalio.Extensions.DiagnosticSource](src/Temporalio.Extensions.DiagnosticSource/README.md) | `System.Diagnostics.Metrics` support for SDK metrics |
| [Temporalio.Extensions.Gcp.CloudRun.Id](src/Temporalio.Extensions.Gcp.CloudRun.Id/README.md) | Client identity derived from Google Cloud Run instance metadata |
| [Temporalio.Extensions.Gcp.CloudRun.OpenTelemetry](src/Temporalio.Extensions.Gcp.CloudRun.OpenTelemetry/README.md) | OpenTelemetry defaults for workers running on Google Cloud Run |
| [Temporalio.Extensions.Hosting](src/Temporalio.Extensions.Hosting/README.md) | Client dependency injection, activity dependency injection, and worker generic host support |
| [Temporalio.Extensions.OpenTelemetry](src/Temporalio.Extensions.OpenTelemetry/README.md) | OpenTelemetry tracing support |
| [Temporalio.Extensions.WorkflowStreams](src/Temporalio.Extensions.WorkflowStreams/README.md) | Experimental durable, offset-based workflow publish/subscribe streams |

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for how to open issues and pull requests, and the [Development](#development)
section below for building and testing this repository.

## Development

### Build

Prerequisites:

* [.NET SDK](https://learn.microsoft.com/en-us/dotnet/core/install/) — version pinned in [`global.json`](global.json)
* [Rust](https://www.rust-lang.org/) (i.e. `cargo` on the `PATH`) — installing via
  [rustup](https://rustup.rs/) is recommended, since the toolchain version is pinned in
  [`src/Temporalio/Bridge/rust-toolchain.toml`](src/Temporalio/Bridge/rust-toolchain.toml) and only
  rustup honors that pin
* [Protobuf Compiler](https://protobuf.dev/) (i.e. `protoc` on the `PATH`)
* [mise](https://mise.jdx.dev/getting-started.html) — running `mise install` in the repository root installs the
  tool versions pinned in [`mise.toml`](mise.toml) and puts them on the `PATH`, matching what CI uses
* This repository, cloned recursively

With all prerequisites in place, run:

    dotnet build

Or for release:

    dotnet build --configuration Release

The native Rust library is built automatically as part of the above, through the Cargo workspace at
[`src/Temporalio/Bridge/Cargo.toml`](src/Temporalio/Bridge/Cargo.toml). That workspace exists so the
library's dependency graph is pinned by a committed
[`Cargo.lock`](src/Temporalio/Bridge/Cargo.lock); `sdk-core` is a published Rust library and
deliberately does not commit one of its own. Its workspace tables and toolchain channel are
mirrored from the submodule, so after bumping `sdk-core` copy over anything that changed upstream
and then run:

    mise run bridge:check-sync  # confirm the mirrored tables match the submodule
    mise run bridge:relock      # refresh Bridge/Cargo.lock, then commit it

### Code formatting

This project uses StyleCop analyzers with some overrides in `.editorconfig`. To format, run:

    dotnet format

Can also run with `--verify-no-changes` to ensure it is formatted.

#### VisualStudio Code

When developing in vscode, the following JSON settings will enable StyleCop analyzers:

```json
    "omnisharp.enableEditorConfigSupport": true,
    "omnisharp.enableRoslynAnalyzers": true
```

### Testing

Run:

    dotnet test

Can add options like:

* `--logger "console;verbosity=detailed"` to show logs
* `--filter "FullyQualifiedName=Temporalio.Tests.Client.TemporalClientTests.ConnectAsync_Connection_Succeeds"` to run a
  specific test
* `--blame-crash` to do a host process dump on crash

To help debug native pieces and show full stdout/stderr, this is also available as an in-proc test program. Run:

    dotnet run --project tests/Temporalio.Tests

Extra args can be added after `--`, e.g. `-- -verbose` would show verbose logs and `-- --help` would show other
options. If the arguments are anything but `--help`, the current assembly is prepended to the args before sending to the
xUnit runner.

Tests are eligible to run against Temporal Cloud unless they have a `CloudTestExclusion` attribute. Run or list the
Cloud-eligible tests with:

    dotnet test tests/Temporalio.Tests --filter "CloudTest!=Excluded"
    dotnet test tests/Temporalio.Tests --list-tests --filter "CloudTest!=Excluded"

To inventory excluded tests, filter on `CloudTest=Excluded`, or filter on
[`CloudTestExclusionReason`](tests/Temporalio.Tests/CloudTestExclusionReason.cs) to inspect a particular category.

The following environment variables can be set to override the environment:

* `TEMPORAL_TEST_CLIENT_TARGET_HOST` - This must be set for any of the variables below to apply
* `TEMPORAL_TEST_CLIENT_NAMESPACE` - Required if the above is set
* `TEMPORAL_TEST_CLIENT_CERT` - Optional, must be present if below is
* `TEMPORAL_TEST_CLIENT_KEY` - Optional, must be present if above is

### Regenerating code

Every generator is a [mise](https://mise.jdx.dev/) task. The .NET tools they need are pinned in
[`.config/dotnet-tools.json`](.config/dotnet-tools.json) and the rest in [`mise.toml`](mise.toml). To regenerate
everything the way CI does:

    mise run gen

Or regenerate a single piece:

* `mise run gen:api` - the `Temporalio.Api.*` protobuf types
* `mise run gen:nexus` - the system Nexus workflow service bindings
* `mise run gen:interop` - the bridge interop layer, from the `sdk-core` C header, using
  [ClangSharpPInvokeGenerator](https://github.com/dotnet/ClangSharp#generating-bindings)

Each task installs the tools it needs on first run, so nothing has to be installed globally. Run `mise tasks` to see
them all. Commit the regenerated output rather than hand-editing generated files; CI runs `mise run gen` and fails if
it produces a diff.

`gen:interop` runs on Windows only, because the generator package ships native `libclang` binaries for Windows alone
(it can be made to work on Linux, but it is annoying to set up). It is ordered last so the other generators still
complete elsewhere.

If you cannot run a generator locally, let CI produce the output for you. The "Regen confirm unchanged" step uploads a
`generator-diff` artifact holding a single `generator.diff`, a unified diff of everything the regen changed. From the
repository root:

    gh run download <run-id> -n generator-diff
    git apply generator.diff

The artifact can also be downloaded as a zip from the Artifacts section of the workflow run page. Review the result and
commit it as you would your own regen.

The Rust DLL itself is built automatically when the project is built, and needs `protoc` on the `PATH` (see
[Build](#build)).

### Regenerating API docs

    mise run docs

This builds the docs with [docfx](https://dotnet.github.io/docfx/) at the version pinned in
[`.config/dotnet-tools.json`](.config/dotnet-tools.json).
