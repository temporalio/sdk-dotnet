# Temporal .NET SDK

⚠️ UNDER ACTIVE DEVELOPMENT

(for the previous .NET SDK repo, see https://github.com/temporalio/experiment-dotnet)

## Quick Start

TODO

## Usage

TODO

## Development

### Build

With `dotnet` installed with all needed frameworks and Rust installed (i.e.
`cargo` on the `PATH`), run:

    dotnet build

Or for release:

    dotnet build --configuration Release

### Code formatting

Install [CSharpier](https://csharpier.com):

    dotnet tool install --global csharpier

Run `dotnet format` then `csharpier`:

    dotnet format style && dotnet format analyzers && dotnet csharpier .

### Testing

Run:

    dotnet test

Can add options like:

* `--logger "console;verbosity=detailed"` to show logs
  * TODO(cretz): This doesn't show Rust stdout. How do I do that?
* `--filter "FullyQualifiedName=Temporalio.Tests.Client.TemporalClientTests.ConnectAsync_Connection_Succeeds"` to run a
  specific test

### Rebuilding Rust extension and interop layer

To rebuild DLL, make sure `protoc` is on the `PATH`, then:

    cargo build --manifest-path src/Temporalio/Bridge/Cargo.toml

To regen core interop from header, install
[ClangSharpPInvokeGenerator](https://github.com/dotnet/ClangSharp#generating-bindings) like:

    dotnet tool install --global ClangSharpPInvokeGenerator

Then, run:

    ClangSharpPInvokeGenerator @src/Temporalio/Bridge/GenerateInterop.rsp

### Regenerating protos

Must have `protoc` on the `PATH`, then:

    dotnet run --project src/Temporalio.Api.Generator

### Regenerating API docs

Install [docfx](https://dotnet.github.io/docfx/), then run:

    docfx src/Temporalio.ApiDoc/docfx.json

TODO:

* Fix generated api doc
  * Specifically make `Temporalio.Api` have children collapsed by default
  * Switch/update template to full width
* Confirm we can detect unused imports
* Build out CI
* Formatting/style guide:
  * Line len 100 max on everything where reasonable
  * Rules for options classes
    * Shallow-copyable via virtual clone
    * Empty constructor and constructor with required params
    * TODO(cretz): Validation? Probably don't want attributes?
  * Rules for triple-slash docs
    * `<summary>` and `<remarks>` tags and end tags are on their own line
    * Although annoying, every param and returns is docd. Can omit return on untyped `Task`.
    * Punctuation on all. Full sentences not required.