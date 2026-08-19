#!/usr/bin/env bash

# Create the Git tag and GitHub release for an already-published SDK version.
#
# NuGet publication and smoke testing happen before this script runs. It is
# deliberately safe to rerun: matching GitHub state is accepted, while a tag or
# release that conflicts with the requested version and commit stops the run.

set -euo pipefail

version_is_newer() {
  local candidate_version=$1
  local current_version=${2#v}
  local -a candidate_parts current_parts
  local index candidate_part current_part

  [[ "$candidate_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || return 1
  [[ "$current_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || return 1
  IFS=. read -r -a candidate_parts <<< "$candidate_version"
  IFS=. read -r -a current_parts <<< "$current_version"
  for index in 0 1 2; do
    candidate_part=$((10#${candidate_parts[$index]}))
    current_part=$((10#${current_parts[$index]}))
    (( candidate_part > current_part )) && return 0
    (( candidate_part < current_part )) && return 1
  done
  return 1
}

select_latest_release() {
  local candidate_version=$1
  shift
  local entry release_id release_tag release_version

  latest_release_id=
  latest_release_tag=
  latest_release_version=
  candidate_found=false
  for entry in "$@"; do
    IFS=$'\t' read -r release_id release_tag <<< "$entry"
    [[ "$release_tag" == "$candidate_version" ]] && candidate_found=true
    release_version=${release_tag#v}
    [[ "$release_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || continue
    if [[ -z "$latest_release_id" ]] || \
       version_is_newer "$release_version" "$latest_release_version"; then
      latest_release_id=$release_id
      latest_release_tag=$release_tag
      latest_release_version=$release_version
    fi
  done
  if [[ "$candidate_found" != true || -z "$latest_release_id" ]]; then
    echo "Unable to find the new release while selecting the latest stable version" >&2
    return 1
  fi
}

reconcile_latest_release() {
  local candidate_version=$1
  local prerelease=$2
  local stable_release_data
  local -a stable_releases

  [[ "$prerelease" == true ]] && return
  [[ "$prerelease" == false ]] || {
    echo "PRERELEASE must be true or false, got: $prerelease" >&2
    return 1
  }

  stable_release_data=$(gh api --paginate \
    "repos/$GITHUB_REPOSITORY/releases?per_page=100" \
    --jq '.[] | select(.draft == false and .prerelease == false) | [.id, .tag_name] | @tsv')
  mapfile -t stable_releases <<< "$stable_release_data"
  select_latest_release "$candidate_version" "${stable_releases[@]}"

  # Every concurrent release independently selects the same highest stable
  # version after it becomes visible. The release created last also reconciles
  # last, so completion order cannot leave an older version marked as latest.
  gh api --method PATCH "repos/$GITHUB_REPOSITORY/releases/$latest_release_id" \
    -f make_latest=true --silent
  echo "GitHub release $latest_release_tag is the latest stable release"
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
    reconcile_latest_release "$version" "$prerelease"
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

  # Defer the latest marker until after creation so concurrent versions can
  # reconcile it deterministically instead of racing read-then-create calls.
  release_args=(--latest=false)
  [[ "$prerelease" == true ]] && release_args=(--prerelease --latest=false)
  gh release create "$version" \
    "${tag_args[@]}" \
    "${release_args[@]}" \
    --title "$version" \
    --notes-file "$release_notes"
  reconcile_latest_release "$version" "$prerelease"
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  main "$@"
fi
