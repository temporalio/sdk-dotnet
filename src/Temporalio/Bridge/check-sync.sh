#!/bin/sh
# Fails if Bridge/Cargo.toml has stopped mirroring sdk-core's workspace tables. A core bump that
# changes them upstream would otherwise leave us building the bridge against different versions than
# upstream tests with, and the build would still succeed: --locked cannot catch it, because our
# lockfile would agree with our own stale manifest.
#
# Run this as 'mise run bridge:check-sync', which puts the pinned yq on the PATH.
set -eu

dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)

if ! command -v yq >/dev/null 2>&1; then
    echo "error: yq not found on the PATH; run 'mise run bridge:check-sync'" >&2
    exit 1
fi

if [ ! -f "$dir/sdk-core/Cargo.toml" ]; then
    echo "error: $dir/sdk-core not populated; run 'git submodule update --init --recursive'" >&2
    exit 1
fi

# The whole manifest is compared except for the keys deleted here, so a table added upstream is
# reported rather than silently skipped:
#   workspace.members, workspace.default-members - ours claims only the crate we build, by its
#                                                  path relative to this manifest.
#   workspace.package.license-file               - upstream names its own LICENSE.txt, ours
#                                                  names this repository's LICENSE.
mirrored='del(
    .workspace.members,
    .workspace["default-members"],
    .workspace.package["license-file"]
) | sort_keys(..)'

# sort_keys leaves only a real difference in the parsed values to reach the diff, never
# formatting, comments or key order.
manifest() {
    yq --input-format toml --output-format json "$mirrored" "$1/Cargo.toml"
}

# Only the channel has to match: rustup reads rust-toolchain.toml from the working directory, so
# ours is the file that governs the bridge build, and upstream's components are for their own CI.
channel() {
    yq --input-format toml --output-format yaml '.toolchain.channel' "$1/rust-toolchain.toml"
}

theirs=$(mktemp)
ours=$(mktemp)
trap 'rm -f "$theirs" "$ours"' EXIT HUP INT TERM

status=0

manifest "$dir/sdk-core" > "$theirs"
manifest "$dir" > "$ours"
if ! diff -u -L "sdk-core/Cargo.toml" "$theirs" -L "Bridge/Cargo.toml" "$ours"; then
    status=1
fi

theirs_channel=$(channel "$dir/sdk-core")
ours_channel=$(channel "$dir")
if [ "$theirs_channel" != "$ours_channel" ]; then
    echo "Rust toolchain channel differs: sdk-core pins '$theirs_channel', Bridge pins" \
        "'$ours_channel'" >&2
    status=1
fi

if [ "$status" -ne 0 ]; then
    cat >&2 <<'EOF'

Bridge/Cargo.toml (and/or Bridge/rust-toolchain.toml) has drifted from the sdk-core submodule.
Copy the changed tables over from src/Temporalio/Bridge/sdk-core/Cargo.toml, then regenerate the
lockfile with 'mise run bridge:relock'.
EOF
    exit 1
fi

echo "Bridge workspace tables are in sync with sdk-core."
