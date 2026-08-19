#!/usr/bin/env bash

# Create the Git tag and GitHub release for an already-published SDK version.
#
# NuGet publication and smoke testing happen before this script runs. It is
# deliberately safe to rerun: matching GitHub state is accepted, while a tag or
# release that conflicts with the requested version and commit stops the run.

set -euo pipefail

set_release_args() {
  local prerelease=$1

  release_args=()
  if [[ "$prerelease" == true ]]; then
    release_args=(--prerelease --latest=false)
  elif [[ "$prerelease" != false ]]; then
    echo "PRERELEASE must be true or false, got: $prerelease" >&2
    return 1
  fi
}

main() {
  local version="${1:?Usage: create-github-release.sh VERSION COMMIT PRERELEASE}"
  local release_commit="${2:?Usage: create-github-release.sh VERSION COMMIT PRERELEASE}"
  local prerelease="${3:?Usage: create-github-release.sh VERSION COMMIT PRERELEASE}"
  local generated_notes notable_changes release release_draft release_name
  local release_notes release_prerelease tag_commit
  local -a release_args tag_args

  : "${GH_TOKEN:?GH_TOKEN must authenticate GitHub CLI requests}"
  : "${GITHUB_REPOSITORY:?GITHUB_REPOSITORY must identify the release repository}"
  : "${RUNNER_TEMP:?RUNNER_TEMP must identify a temporary output directory}"
  [[ "$prerelease" == true || "$prerelease" == false ]] || {
    echo "PRERELEASE must be true or false, got: $prerelease" >&2
    exit 1
  }

  notable_changes="$RUNNER_TEMP/notable-changes.md"
  generated_notes="$RUNNER_TEMP/generated-notes.md"
  release_notes="$RUNNER_TEMP/release-notes.md"

  # Refuse to reuse a version tag from another commit. If the tag does not
  # exist, `gh release create` creates it at the immutable release commit.
  tag_args=(--target "$release_commit")
  if git rev-parse --verify --quiet "refs/tags/$version" >/dev/null; then
    tag_commit=$(git rev-list -n 1 "$version")
    if [[ "$tag_commit" != "$release_commit" ]]; then
      echo "Tag $version points to $tag_commit, expected $release_commit" >&2
      exit 1
    fi
    tag_args=(--verify-tag)
  fi

  # A successful rerun must not replace or silently modify an existing release.
  # Accept only the public release shape this script itself would have created.
  if release=$(gh api "repos/$GITHUB_REPOSITORY/releases/tags/$version" 2>/dev/null); then
    release_name=$(jq -r .name <<< "$release")
    release_draft=$(jq -r .draft <<< "$release")
    release_prerelease=$(jq -r .prerelease <<< "$release")
    if [[ "$release_name" != "$version" ||
          "$release_draft" != false ||
          "$release_prerelease" != "$prerelease" ]]; then
      echo "GitHub release $version exists with conflicting metadata" >&2
      exit 1
    fi
    echo "GitHub release $version already exists at the expected tag"
    exit 0
  fi

  # Preserve the curated Unreleased changelog text as the first section. The
  # next numbered release heading marks its end; ordinary Added/Changed/etc.
  # headings remain part of the selected content.
  awk '
    /^## \[Unreleased\]$/ { found=1; next }
    found && /^#{2,3} \[[0-9]/ { exit }
    found { print }
  ' CHANGELOG.md > "$notable_changes"
  if ! grep -q '[^[:space:]]' "$notable_changes"; then
    echo "CHANGELOG.md has no Unreleased release notes" >&2
    exit 1
  fi

  # Append GitHub's contributor and comparison notes to the curated highlights.
  gh api --method POST "repos/$GITHUB_REPOSITORY/releases/generate-notes" \
    -f tag_name="$version" \
    -f target_commitish="$release_commit" \
    --jq .body > "$generated_notes"
  {
    echo "## Notable Changes"
    cat "$notable_changes"
    echo
    cat "$generated_notes"
  } > "$release_notes"

  # Stable releases use GitHub's server-side default, which selects latest
  # automatically by date and semantic version without a client-side race.
  # Prereleases must be explicitly excluded from latest selection.
  set_release_args "$prerelease"
  gh release create "$version" \
    "${tag_args[@]}" \
    "${release_args[@]}" \
    --title "$version" \
    --notes-file "$release_notes"
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  main "$@"
fi
