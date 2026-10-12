#!/usr/bin/env bash
# Fetches everything build-apk.sh needs, from public sources only (no Google Maven / dl.google.com):
#   * Android framework classes for API 34 (Maven Central: org.robolectric:android-all) - used as android.jar
#   * R8/D8 (storage.googleapis.com/r8-releases)
#   * Kotlin compiler jars (Maven Central)
# and checks that aapt, apksigner, zipalign, zip, javac are on PATH (Debian/Ubuntu: apt install aapt apksigner zipalign zip openjdk-17-jdk-headless).
set -euo pipefail

TOOLS="${LMX_TOOLCHAIN:-/opt/lmx-android}"
KOTLIN_VER=2.0.21
CENTRAL=https://repo1.maven.org/maven2
mkdir -p "$TOOLS/dl" "$TOOLS/kotlin"

fetch() { # url dest [sha1]
  local url="$1" dest="$2" sha="${3:-}"
  if [ -f "$dest" ]; then return; fi
  echo "fetching $(basename "$dest")"
  for try in 1 2 3 4; do
    curl -fsSL --retry 3 -o "$dest.tmp" "$url" && break
    echo "  retry $try (Maven Central rate-limits aggressively)"; sleep $((try * 5))
  done
  [ -s "$dest.tmp" ] || { echo "download failed: $url"; exit 1; }
  if [ -n "$sha" ]; then echo "$sha  $dest.tmp" | sha1sum -c - >/dev/null || { echo "checksum mismatch: $dest"; exit 1; }; fi
  mv "$dest.tmp" "$dest"
}

fetch "$CENTRAL/org/robolectric/android-all/14-robolectric-10818077/android-all-14-robolectric-10818077.jar" \
      "$TOOLS/dl/android-all-34.jar" 94b1490a891e9be559aa35c87cd8a0c163f32d83
fetch "https://storage.googleapis.com/r8-releases/raw/8.5.35/r8lib.jar" "$TOOLS/dl/r8lib.jar"

for a in kotlin-compiler-embeddable kotlin-stdlib kotlin-script-runtime kotlin-reflect kotlin-daemon-embeddable; do
  fetch "$CENTRAL/org/jetbrains/kotlin/$a/$KOTLIN_VER/$a-$KOTLIN_VER.jar" "$TOOLS/kotlin/$a-$KOTLIN_VER.jar"
done
fetch "$CENTRAL/org/jetbrains/intellij/deps/trove4j/1.0.20200330/trove4j-1.0.20200330.jar" "$TOOLS/kotlin/trove4j-1.0.20200330.jar"
fetch "$CENTRAL/org/jetbrains/annotations/13.0/annotations-13.0.jar" "$TOOLS/kotlin/annotations-13.0.jar"
fetch "$CENTRAL/org/jetbrains/kotlinx/kotlinx-coroutines-core-jvm/1.6.4/kotlinx-coroutines-core-jvm-1.6.4.jar" "$TOOLS/kotlin/kotlinx-coroutines-core-jvm-1.6.4.jar"

missing=0
for t in aapt apksigner zipalign zip javac keytool java; do
  command -v "$t" >/dev/null || { echo "missing tool: $t"; missing=1; }
done
[ $missing -eq 0 ] || { echo "install: sudo apt install aapt apksigner zipalign zip openjdk-17-jdk-headless"; exit 1; }
echo "toolchain ready in $TOOLS"
