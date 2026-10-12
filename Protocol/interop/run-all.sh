#!/usr/bin/env bash
# Builds everything, brings the lab up and runs every interop test. Needs root (network namespaces),
# qt6-base-dev + libssl-dev (for the original client), python3, iproute2, the Kotlin toolchain
# (AndroidClient/tools/setup-toolchain.sh) and the .NET 8 SDK.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
./build.sh
./lab.sh down 2>/dev/null || true
./lab.sh up
trap './lab.sh down' EXIT
fail=0
for c in kotlin csharp; do echo "=== $c vs original ==="; CLIENT=$c python3 test_legacy.py | grep -E '^(PASS|FAIL|RESULT)' || fail=1; done
echo "=== kotlin vs csharp ==="; python3 test_kotlin_csharp.py | grep -E '^(PASS|FAIL|RESULT)' || fail=1
for c in kotlin csharp; do echo "=== file stress: $c vs original ==="; CLIENT=$c python3 test_files.py | grep -E '^(FAIL|RESULT)' || fail=1; done
exit $fail
