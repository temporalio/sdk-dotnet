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

Complete dependency, Core, and lockfile updates before preparing a release. Use a
clean, recursively cloned checkout with the .NET 10 SDK, Git, authenticated GitHub
CLI (`gh auth login`), mise (`mise install`), and rustup available. The pinned Rust
toolchain is required for the shared changelog tool; no Python or PowerShell is
needed. Ensure `origin` points to the intended GitHub repository and your account
can push branches and open pull requests there.

```bash
dotnet run --file scripts/prepare-release.cs -- 1.21.0 --date 2026-10-06
```

**This command publishes a branch and opens a pull request.** The date defaults to
today if omitted. It validates inputs, requires a clean worktree (including
untracked files and submodules), fetches `origin/main`, creates
`chore/release-<version>`, and initializes submodules. It updates `Version` and
`AssemblyVersion`, then runs `mise run changelog:prepare` to prepend a dated
section to `CHANGELOG.md` and remove consumed fragments. Releases without
fragments are allowed. Only the version file, changelog, and deleted fragments
may be committed; unexpected changes stop preparation. It pushes the branch to
`origin` and creates a PR against `main` with the `skip-changelog` label.

Release versions use `major.minor.patch` with an optional SemVer prerelease
suffix, without build metadata. Numeric assembly components must be 0..65534.
Ordinary stable releases use `major.minor.patch` for `AssemblyVersion`.
Prereleases require an explicit positive revision, matching the conventions in
`Directory.Build.props`:

```bash
dotnet run --file scripts/prepare-release.cs -- 1.21.0-alpha.1 --assembly-version 1.21.0.1
```

Increment the revision for each prerelease. The final stable release after a
prerelease also needs a greater revision; the script automatically increments it
when the current assembly version has the same `major.minor.patch`. You can
override this with `--assembly-version`, for example if intervening development
changes mean the previous prerelease revision is no longer in the file.

The script deliberately leaves `PackageValidationBaselineVersion` unchanged:
`dotnet pack` validates against a **previously published** NuGet package, so
setting the baseline to the release being prepared would fail before publication
and would no longer check compatibility with the previous release. Verify that
the retained baseline is the intended published package; update it separately
after publication when preparing subsequent development, not to an unpublished
version.

Review the PR's versions, changelog, and fragment deletions, run the usual
validation (including `dotnet test` and `dotnet pack -c Debug`), and merge only
after CI passes. If preparation fails, inspect the branch and worktree before
retrying: the script does not undo changes, delete branches, or force-push.
If pushing succeeded but PR creation failed, create the PR manually with
`skip-changelog` rather than rerunning preparation.

To test preparation and package verification without fetching, creating branches,
pushing, or contacting GitHub:

```bash
dotnet run --file scripts/prepare-release.cs -- --self-test
dotnet run --file scripts/release-verify.cs -- --self-test
```

CI runs these checks on each supported runner platform. Package verification tests
use temporary synthetic packages, not published packages.

## Publishing a Release

After the preparation PR merges, dispatch **Release Publish**
(`.github/workflows/release-publish.yml`) from `main`. Confirm the selected commit
contains the intended versions and completed changelog section. The workflow
validates release metadata before building, verifies that the artifact contains
every packable SDK project at that version with matching symbol packages, then
publishes the same verified artifact to the integration and production galleries.
Integration smoke tests must pass before production publishing, and production
smoke tests must pass before draft release creation.

The workflow creates a **draft** GitHub release with a tag targeting the dispatched
commit, using notes from the shared changelog tool. Review the draft's tag,
changelog, Core commit section, and prerelease status before publishing the GitHub
release. Also check each gallery's package-owner page for symbol validation and
indexing failures: the automated availability check verifies runtime packages,
not symbol indexing. NuGet has no supported public symbol-package availability
API. NuGet publishing has already happened at this point; editing or deleting the
draft does not roll back packages. Do not move the release tag or overwrite
published packages to recover from a failure.

Publishing fails on duplicate versions rather than silently accepting packages
from a different commit. If a batch fails after some uploads, retain the verified
`release-package` artifact and reconcile the gallery using those exact files;
complete any missing runtime or symbol uploads through the gallery's owner UI.
An automatic full rerun is not a recovery mechanism for a partially published
batch: rerunning its failed publish job will still reject the already-published
packages. Complete the remaining smoke checks and draft release manually in that
case. If publishing succeeded but a later smoke test or draft creation failed,
rerun only the failed downstream jobs after diagnosing the failure; do not rebuild
or republish.

For local review or recovery, generate notes manually:

```bash
mise run changelog:release-notes -- --version 1.21.0 --output release-notes.md
```

This combines the completed changelog section with a `SDK Core Commits` section
covering Core changes since the previous version tag. If needed, supply explicit
SDK refs with `--from <previous-tag> --to <release-ref>`. The output path is relative
to the repository root. Do not commit the generated `release-notes.md`.

### Trusted Publishing Setup

Maintainers must configure the galleries and GitHub environments before the first
release; repository changes alone cannot create these policies. Follow
[NuGet's trusted publishing guidance](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
for both `int.nugettest.org` and `nuget.org`, using the `Temporal` NuGet profile
used by the publishing workflow (a profile name, not an email address). Ensure
that profile is authorized for the package owner, and scope policies to the SDK
package IDs, including all extension packages.

Configure a policy in each gallery:

| Setting | Integration gallery | Production gallery |
| --- | --- | --- |
| Repository owner | `temporalio` | `temporalio` |
| Repository | `sdk-dotnet` | `sdk-dotnet` |
| Workflow file | `nuget-publish.yml` | `nuget-publish.yml` |
| Environment | `nugetint` | `nugetprod` |

Enter only the workflow filename, without `.github/workflows/`. Publishing runs
inside the same-repository reusable `nuget-publish.yml`, invoked by
`release-publish.yml`. NuGet matches the token's `job_workflow_ref` (the publishing
job's workflow), so use **`nuget-publish.yml`, not the caller's
`release-publish.yml`**. This behavior is implemented in
[NuGet's policy validator](https://github.com/NuGet/NuGetGallery/blob/main/src/NuGetGallery.Services/Authentication/Federated/GitHubTokenPolicyValidator.cs).
Recheck the policy if the publishing job is moved to a different workflow.

In GitHub repository settings, protect the existing `nugetint` and `nugetprod`
environments with required reviewers and deployment branch rules allowing only
`main`. In particular, require approval before production publishing; prevent
self-approval or administrator bypass where supported by the repository's plan.
Before approving `nugetprod`, check integration-gallery symbol indexing; check
production symbol indexing again when reviewing the GitHub draft.
Do not leave the gallery policy's environment field empty: environment
restrictions bind token exchange to those protected jobs.

The workflow exchanges GitHub OIDC tokens for short-lived NuGet API keys.
No stored NuGet API key or personal access token is needed for publishing.
Local `gh auth login` is only needed by release preparation to push/open its PR;
the workflow uses its scoped `GITHUB_TOKEN` for draft release creation.

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
