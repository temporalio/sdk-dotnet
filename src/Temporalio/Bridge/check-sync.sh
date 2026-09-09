#!/bin/sh
# Fails if Bridge/Cargo.toml has stopped mirroring sdk-core's workspace tables. A core bump that
# changes them upstream would otherwise leave us building the bridge against different versions than
# upstream tests with, and the build would still succeed: --locked cannot catch it, because our
# lockfile would agree with our own stale manifest.
set -eu

dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
ours="$dir/Cargo.toml"
theirs="$dir/sdk-core/Cargo.toml"

if [ ! -f "$theirs" ]; then
    echo "error: $theirs not found; run 'git submodule update --init --recursive'" >&2
    exit 1
fi

# Continuation lines of a multi-line value are always indented, so a leading "[" only ever starts a
# table header and can be used to find both ends of a table.
extract() {
    awk -v want="$2" 'substr($0, 1, 1) == "[" { inside = ($0 == want) } inside' "$1"
}

table_names() {
    awk 'substr($0, 1, 1) == "[" { print }' "$1" | sort
}

status=0

# Compared first so that a table added upstream which we mirror nowhere is reported, rather than
# silently skipped by the per-table loop below.
a=$(mktemp)
b=$(mktemp)
table_names "$theirs" > "$a"
table_names "$ours" > "$b"
if ! diff -u -L "sdk-core/Cargo.toml tables" "$a" -L "Bridge/Cargo.toml tables" "$b"; then
    status=1
fi
rm -f "$a" "$b"

# [workspace.package] is omitted: upstream's license-file names its own LICENSE.txt, ours names this
# repo's LICENSE.
for section in \
    '[workspace.dependencies]' \
    '[workspace.lints.rust]' \
    '[workspace.lints.clippy]' \
    '[profile.release-lto]'; do
    a=$(mktemp)
    b=$(mktemp)
    extract "$theirs" "$section" > "$a"
    extract "$ours" "$section" > "$b"
    if ! diff -u -L "sdk-core/Cargo.toml $section" "$a" -L "Bridge/Cargo.toml $section" "$b"; then
        status=1
    fi
    rm -f "$a" "$b"
done

channel() {
    sed -n 's/^[[:space:]]*channel[[:space:]]*=[[:space:]]*"\([^"]*\)".*/\1/p' "$1"
}
theirs_channel=$(channel "$dir/sdk-core/rust-toolchain.toml")
ours_channel=$(channel "$dir/rust-toolchain.toml")
if [ "$theirs_channel" != "$ours_channel" ]; then
    echo "Rust toolchain channel differs: sdk-core pins '$theirs_channel', Bridge pins '$ours_channel'"
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
