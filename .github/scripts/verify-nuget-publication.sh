#!/usr/bin/env bash

# Wait for every release package to reach a NuGet V3 flat container and verify
# that each published package came from the exact workflow artifact being
# released. This makes retries safe: an existing version is accepted only when
# its immutable content matches the candidate artifact.

set -euo pipefail

readonly package_ids=(
  temporalio
  temporalio.extensions.aws.lambda
  temporalio.extensions.aws.lambda.opentelemetry
  temporalio.extensions.diagnosticsource
  temporalio.extensions.gcp.cloudrun.opentelemetry
  temporalio.extensions.hosting
  temporalio.extensions.opentelemetry
)

package_content_hash() {
  local package=$1
  local signature_file=$2
  local signature_content hash_oid hash_algorithm expected_hash
  local -a hash_lines

  unzip -p "$package" .signature.p7s > "$signature_file"
  signature_content=$(openssl cms -verify -binary -inform DER -noverify \
    -in "$signature_file" 2>/dev/null)
  mapfile -t hash_lines < <(
    sed -n 's/^[[:space:]]*\([0-9.]*\)-Hash:\([^[:space:]]*\)[[:space:]]*$/\1 \2/p' \
      <<< "$signature_content"
  )
  if [[ ${#hash_lines[@]} -ne 1 ]]; then
    echo "Published package has an invalid NuGet repository signature" >&2
    return 1
  fi
  read -r hash_oid expected_hash <<< "${hash_lines[0]}"
  case "$hash_oid" in
    2.16.840.1.101.3.4.2.1) hash_algorithm=sha256 ;;
    2.16.840.1.101.3.4.2.2) hash_algorithm=sha384 ;;
    2.16.840.1.101.3.4.2.3) hash_algorithm=sha512 ;;
    *)
      echo "Published package uses an unsupported content hash OID: $hash_oid" >&2
      return 1
      ;;
  esac
  printf '%s %s\n' "$hash_algorithm" "$expected_hash"
}

verify_published_package() {
  local local_package=$1
  local published_package=$2
  local scratch_dir=$3
  local actual_hash expected_hash hash_algorithm signature_file

  unzip -tqq "$local_package" >/dev/null
  unzip -tqq "$published_package" >/dev/null
  if unzip -Z1 "$local_package" | grep -Fxq .signature.p7s; then
    echo "The workflow artifact is already signed; cannot derive its unsigned content hash" >&2
    return 1
  fi

  if unzip -Z1 "$published_package" | grep -Fxq .signature.p7s; then
    signature_file="$scratch_dir/published-signature.p7s"
    read -r hash_algorithm expected_hash < <(
      package_content_hash "$published_package" "$signature_file"
    )
    actual_hash=$(openssl dgst "-$hash_algorithm" -binary "$local_package" | openssl base64 -A)
    [[ "$actual_hash" == "$expected_hash" ]]
  else
    # Test galleries may not repository-sign packages. In that case the bytes
    # themselves must be identical to the artifact that was pushed.
    cmp -s "$local_package" "$published_package"
  fi
}

find_local_package() {
  local artifact_dir=$1
  local package_id=$2
  local version=$3
  local extension=$4
  local -a matches

  mapfile -t matches < <(
    find "$artifact_dir" -type f -iname "$package_id.$version.$extension"
  )
  if [[ ${#matches[@]} -ne 1 ]]; then
    echo "Expected exactly one $extension artifact for $package_id $version, found ${#matches[@]}" >&2
    return 1
  fi
  printf '%s\n' "${matches[0]}"
}

main() {
  local usage="Usage: verify-nuget-publication.sh ARTIFACT_DIR INDEX_URL SYMBOL_PACKAGE_BASE_URL VERSION"
  local artifact_dir="${1:?$usage}"
  local index_url="${2:?$usage}"
  local symbol_package_base_url="${3:?$usage}"
  local version="${4:?$usage}"
  local host package_id package_url local_package published_package
  local symbol_package_url local_symbol_package published_symbol_package
  local deadline=${NUGET_WAIT_DEADLINE_SECONDS:-1800}
  local interval=${NUGET_WAIT_INTERVAL_SECONDS:-15}
  local scratch_dir
  local -a remaining pending

  [[ "$index_url" =~ ^https://[^/]+/ ]] || {
    echo "NuGet index URL must use HTTPS and include a path: $index_url" >&2
    exit 1
  }
  [[ "$symbol_package_base_url" =~ ^https://[^/]+/[^/]+$ ]] || {
    echo "Symbol package base URL must use HTTPS: $symbol_package_base_url" >&2
    exit 1
  }
  [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$ ]] || {
    echo "Invalid NuGet version: $version" >&2
    exit 1
  }

  # Both galleries used by this workflow expose the flat container on the V3
  # index host. Package IDs and versions are lowercase in flat-container URLs.
  host="${index_url#https://}"
  host="${host%%/*}"
  version=${version,,}
  scratch_dir=$(mktemp -d)
  trap 'rm -rf "$scratch_dir"' EXIT
  remaining=("${package_ids[@]}")
  deadline=$(( $(date +%s) + deadline ))

  while (( ${#remaining[@]} > 0 )); do
    pending=()
    for package_id in "${remaining[@]}"; do
      package_url="https://$host/v3-flatcontainer/$package_id/$version/$package_id.$version.nupkg"
      published_package="$scratch_dir/$package_id.nupkg"
      if ! curl -fsSL "$package_url" -o "$published_package" 2>/dev/null; then
        pending+=("$package_id")
        continue
      fi
      local_package=$(find_local_package "$artifact_dir" "$package_id" "$version" nupkg)
      if ! verify_published_package "$local_package" "$published_package" "$scratch_dir"; then
        echo "Published $package_id $version does not match the workflow artifact" >&2
        exit 1
      fi

      symbol_package_url="$symbol_package_base_url/$package_id.$version.snupkg"
      published_symbol_package="$scratch_dir/$package_id.snupkg"
      if ! curl -fsSL "$symbol_package_url" -o "$published_symbol_package" 2>/dev/null; then
        pending+=("$package_id")
        continue
      fi
      local_symbol_package=$(find_local_package "$artifact_dir" "$package_id" "$version" snupkg)
      if ! verify_published_package \
          "$local_symbol_package" "$published_symbol_package" "$scratch_dir"; then
        echo "Published symbols for $package_id $version do not match the workflow artifact" >&2
        exit 1
      fi
      echo "  $package_id $version runtime and symbol packages match the workflow artifact"
    done
    remaining=("${pending[@]}")
    (( ${#remaining[@]} == 0 )) && break
    if (( $(date +%s) >= deadline )); then
      echo "Timed out on $host; still not available:" >&2
      printf '  - %s\n' "${remaining[@]}" >&2
      exit 1
    fi
    echo "Pending on $host:"
    printf '  - %s\n' "${remaining[@]}"
    echo "Waiting for ${interval}s..."
    sleep "$interval"
  done
  echo "All runtime and symbol packages $version match the workflow artifact"
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  main "$@"
fi
