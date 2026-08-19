#!/usr/bin/env bash

# Exercise the retry decisions that cannot safely be tested against real NuGet
# or GitHub releases. All external state is represented by temporary fixtures
# and a mocked GitHub CLI response.

set -euo pipefail

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
readonly script_dir
# The paths are resolved from this script so the test works from any directory.
# shellcheck disable=SC1091
source "$script_dir/create-github-release.sh"
# shellcheck disable=SC1091
source "$script_dir/verify-nuget-publication.sh"

test_dir=$(mktemp -d)
readonly test_dir
trap 'rm -rf "$test_dir"' EXIT
export GITHUB_REPOSITORY=temporalio/sdk-dotnet
export RUNNER_TEMP="$test_dir"
declare -a release_args

set_release_args false
if (( ${#release_args[@]} != 0 )); then
  echo "Stable releases must leave latest selection to GitHub" >&2
  exit 1
fi

set_release_args true
if [[ "${release_args[*]}" != '--prerelease --latest=false' ]]; then
  echo "Prereleases must be excluded from latest selection" >&2
  exit 1
fi

if set_release_args invalid 2>/dev/null; then
  echo "Invalid prerelease metadata was accepted" >&2
  exit 1
fi

mkdir -p "$test_dir/local" "$test_dir/package-content" "$test_dir/signed-content"
printf 'candidate package content\n' > "$test_dir/package-content/content.txt"
(cd "$test_dir/package-content" && zip -q "$test_dir/local/package.nupkg" content.txt)
cp "$test_dir/local/package.nupkg" "$test_dir/unsigned-published.nupkg"
verify_published_package \
  "$test_dir/local/package.nupkg" "$test_dir/unsigned-published.nupkg" "$test_dir"

content_hash=$(openssl dgst -sha256 -binary "$test_dir/local/package.nupkg" | openssl base64 -A)
printf 'Version:1\n\n2.16.840.1.101.3.4.2.1-Hash:%s\n' "$content_hash" \
  > "$test_dir/signature-content.txt"
openssl req -x509 -newkey rsa:2048 -nodes -subj /CN=release-test \
  -keyout "$test_dir/key.pem" -out "$test_dir/cert.pem" -days 1 2>/dev/null
openssl cms -sign -binary -nodetach -in "$test_dir/signature-content.txt" \
  -signer "$test_dir/cert.pem" -inkey "$test_dir/key.pem" -outform DER \
  -out "$test_dir/signed-content/.signature.p7s"
cp "$test_dir/local/package.nupkg" "$test_dir/signed-published.nupkg"
(cd "$test_dir/signed-content" && zip -q "$test_dir/signed-published.nupkg" .signature.p7s)
verify_published_package \
  "$test_dir/local/package.nupkg" "$test_dir/signed-published.nupkg" "$test_dir"

printf 'different package content\n' > "$test_dir/package-content/content.txt"
(cd "$test_dir/package-content" && zip -q "$test_dir/local/conflict.nupkg" content.txt)
if verify_published_package \
    "$test_dir/local/conflict.nupkg" "$test_dir/signed-published.nupkg" "$test_dir"; then
  echo "A conflicting published package was incorrectly accepted" >&2
  exit 1
fi

echo "Release automation tests passed"
