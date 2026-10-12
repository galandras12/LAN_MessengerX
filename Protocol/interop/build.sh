#!/usr/bin/env bash
# Builds everything the interop tests drive: the original client wrapped headless, and the Kotlin and C# command line clients.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
TOOLS="${LMX_TOOLCHAIN:-/opt/lmx-android}"

echo "==> original client (Qt, headless)"
mkdir -p "$HERE/legacy-harness/build" && (cd "$HERE/legacy-harness/build" && qmake6 ../harness.pro >/dev/null && make -j"$(nproc)" >/dev/null)

echo "==> Kotlin CLI"
K="$TOOLS/kotlin"
KCP="$K/kotlin-compiler-embeddable-2.0.21.jar:$K/kotlin-stdlib-2.0.21.jar:$K/kotlin-script-runtime-2.0.21.jar:$K/kotlin-reflect-2.0.21.jar:$K/kotlin-daemon-embeddable-2.0.21.jar:$(ls "$K"/trove4j-*.jar):$(ls "$K"/annotations-*.jar | head -1):$(ls "$K"/kotlinx-coroutines-core-jvm-*.jar | head -1)"
rm -rf "$ROOT/AndroidClient/build/cli-classes"; mkdir -p "$ROOT/AndroidClient/build"
java -cp "$KCP" org.jetbrains.kotlin.cli.jvm.K2JVMCompiler -no-stdlib -no-reflect -jvm-target 17 -Xjvm-default=all \
  -cp "$K/kotlin-stdlib-2.0.21.jar" -d "$ROOT/AndroidClient/build/cli-classes" \
  $(find "$ROOT/AndroidClient/core/src/main" "$ROOT/AndroidClient/core/src/tool" -name '*.kt') 2>&1 | grep -v "^Picked up" || true

echo "==> C# CLI"
dotnet build "$ROOT/DesktopClient/tools/LanMessengerX.Cli" -c Release -v q --nologo | tail -1
echo "done"
