# Contributing to Temporal SDKs

Thanks for your interest in contributing to Temporal SDKs.

This guide describes expectations that apply across Temporal SDK repositories. Each
repository may have additional local conventions, but the guidance below should help
you open issues and pull requests that maintainers can evaluate efficiently.

## Before You Open an Issue

Search the existing issues first. If you find an issue that describes the same bug,
feature request, or design topic, add any relevant details there instead of opening a
duplicate. Use an upvote on the issue to show that it affects you too.

Issues are assigned to people when they are actively working on them. Before taking
on an issue, check whether it is already assigned so you do not duplicate someone
else's work.

Use GitHub issues for actionable bugs and feature work. For usage questions, help
debugging an application, or general discussion, join the relevant
language-specific channel in the
[Temporal community Slack](https://temporal.io/slack) or use the support channel
available to you.

## Bug Reports

When reporting a bug, include enough detail for someone else to reproduce or
understand the problem:

* A short summary of the problem.
* A minimal reproduction, preferably as code that can be copied into a small
  project or test.
* What you expected to happen and what actually happened.
* The SDK version.
* The language runtime version.
* The operating system and architecture.
* Temporal Server or Temporal Cloud details, if the issue depends on service
  behavior.
* Logs, stack traces, workflow histories, or other diagnostics that show the
  failure.
* Whether the behavior is a regression, and the last version where it worked if
  known.

## Feature Requests and Design Changes

Open or join a GitHub issue before starting substantial feature work, behavior
changes, or API design changes. This gives maintainers and other SDK users a chance
to discuss the approach before you invest in a larger implementation.

The relevant language-specific channel in Temporal community Slack is also a good
place for early discussion, but important decisions should still be captured in a
GitHub issue so they are visible and searchable.

Small bug fixes, documentation fixes, and narrowly scoped maintenance changes can go
straight to a pull request.

## Pull Requests

Good pull requests are focused and easy to review:

* Keep each pull request scoped to one logical change.
* Include tests for behavior changes.
* Update public API documentation or doc comments when public behavior changes.
* Add a high-level changelog entry for user-facing changes according to the
  repository's local changelog convention.
* Describe what changed, why it changed, and what validation you ran.

Run the relevant local checks when practical. CI must pass before a pull request can
be merged.

## Changelog Entries

For user-facing changes, add a fragment in the appropriate category under
`changelog/`, following [the fragment guide](changelog/README.md). Choose a fun,
whimsical filename and keep each entry concise, ideally one or two sentences.
Each nonempty line becomes one bullet; omit bullet markers and keep each entry on
one line. Do not add pending entries to `CHANGELOG.md`.

Internal changes can use the `skip-changelog` pull request label. CI otherwise
requires a new fragment and validates its format.

## Updating SDK Core

Run `mise run core:update` to update the submodule to the latest Core `main` and
import changes since the current pin into categorized fragments. Imported entries
use a `Core: ` prefix to distinguish them from .NET SDK changes. To choose a
specific fetched Core revision, use `mise run core:update -- --revision <revision>`.
The shared tool rejects dirty submodules and backwards or divergent updates.

After updating, mirror any changes in `sdk-core/Cargo.toml` and
`sdk-core/rust-toolchain.toml` into `src/Temporalio/Bridge/Cargo.toml` and
`src/Temporalio/Bridge/rust-toolchain.toml`. Run `mise run bridge:check-sync`, then
`mise run bridge:relock`. Regenerate code with `mise run gen` (the interop generator
requires Windows), review the imported fragments, and commit the submodule pin,
lockfile, generated code, and fragments together.

## Preparing a Release

Update `Version` and `AssemblyVersion` in `Directory.Build.props`, following the
assembly-version guidance there. Keep `PackageValidationBaselineVersion` at the
previously published package used for compatibility validation. Complete any
dependency and lockfile updates before preparing the changelog.

```bash
mise run changelog:prepare -- --version 1.21.0 --date 2026-10-06
```

The shared tool prepends a dated release section to `CHANGELOG.md` and removes the
consumed fragments. Releases with no fragments are allowed. Review and commit the
result with the version updates; a release preparation PR can use `skip-changelog`.

After the release commit and tag are ready, generate notes manually:

```bash
mise run changelog:release-notes -- --version 1.21.0 --output release-notes.md
```

This combines the completed changelog section with a `SDK Core Commits` section
covering Core changes since the previous version tag. If needed, supply explicit
SDK refs with `--from <previous-tag> --to <release-ref>`. The output path is relative
to the repository root. Use the output when creating the GitHub release; the
current NuGet publishing workflow does not create a GitHub release.

## Things to Avoid

Avoid changes that make review harder without improving the contribution:

* Unrelated refactors mixed into a behavior change.
* Style-only churn.
* Large feature pull requests that were not discussed first.
* License, copyright, or other legal changes without maintainer discussion.

## AI-Generated Contributions

Using AI tools while contributing is acceptable. You are responsible for the
correctness, quality, and maintainability of everything you submit.

Contributors must fully understand the issue they are fixing and be able to explain
the proposed change. We expect that human understanding to be evident in pull
request responses and design discussions. If a contribution's interaction appears
entirely AI-driven, maintainers may close it: it does not provide a benefit over
maintainers using AI tooling themselves.

Thoroughly self-review AI-generated code and documentation before opening a pull
request. Make sure it is correct, tested where appropriate, and consistent with the
style and patterns of the codebase.

Keep AI-assisted changes concise and scoped. Avoid verbose generated prose,
unnecessary comments, or broad rewrites that make the change harder to review.

## Contributor License Agreement

All contributors must complete the Temporal Contributor License Agreement (CLA)
before changes can be merged. A link to the CLA will be posted in the pull request.

## Security Issues

Do not open public GitHub issues for suspected security vulnerabilities. Report them
to security@temporal.io instead.

## Review and CI

Maintainers review pull requests for correctness, compatibility, test coverage,
documentation, and long-term maintainability. Review may require changes before a
pull request can be merged, and it may take maintainers some time to review a
contribution.

CI is the final validation gate. If CI fails, update the pull request or ask for help
if the failure appears unrelated to your change. Some CI gates may wait for a
maintainer to approve or run them.

## Inactive Pull Requests

Maintainers may close inactive pull requests after follow-up if they are no longer
moving forward. If that happens, you are welcome to reopen the pull request or open a
new one when you are ready to continue.

## Community Conduct

Keep discussions respectful, constructive, and focused on the work. Clear context,
specific examples, and patience with review feedback help everyone move faster.
