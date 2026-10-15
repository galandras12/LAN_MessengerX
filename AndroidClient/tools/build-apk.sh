#!/usr/bin/env bash
# Builds a signed, shrunk release APK of the LAN Messenger X Android client WITHOUT Gradle:
#   Kotlin compiler -> R8 -> aapt -> zipalign -> apksigner.
# Use setup-toolchain.sh once to fetch the pieces. Output: AndroidClient/build/LANMessengerX-<version>.apk
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
TOOLS="${LMX_TOOLCHAIN:-/opt/lmx-android}"
ANDROID_JAR="${ANDROID_JAR:-$TOOLS/dl/android-all-34.jar}"
R8_JAR="${R8_JAR:-$TOOLS/dl/r8lib.jar}"
KOTLIN_LIBS="${KOTLIN_LIBS:-$TOOLS/kotlin}"           # directory holding kotlin-compiler-embeddable & friends (setup-toolchain.sh)
KOTLIN_VER="${KOTLIN_VER:-2.0.21}"
MIN_SDK=26
TARGET_SDK=34
APP_ID=com.lanmessengerx.android

KEYSTORE="${LMX_KEYSTORE:-$ROOT/signing/lanmessengerx-release.jks}"
KS_PASS="${LMX_KS_PASS:-lanmessengerx}"
KEY_ALIAS="${LMX_KEY_ALIAS:-lanmessengerx}"

# shellcheck disable=SC1091
source <(grep -E '^(versionName|versionCode)=' "$ROOT/version.properties")
filt() { grep -v "^Picked up" || true; }   # grep -v exits 1 on empty input, which would kill a set -e -o pipefail script
JAVA_HOME_DIR="${JAVA_HOME:-$(dirname "$(dirname "$(readlink -f "$(command -v java)")")")}"
OUT="$ROOT/build"
rm -rf "$OUT"; mkdir -p "$OUT"/{gen,rclasses,classes,dex}

KCP="$KOTLIN_LIBS/kotlin-compiler-embeddable-$KOTLIN_VER.jar:$KOTLIN_LIBS/kotlin-stdlib-$KOTLIN_VER.jar:$KOTLIN_LIBS/kotlin-script-runtime-$KOTLIN_VER.jar:$KOTLIN_LIBS/kotlin-reflect-$KOTLIN_VER.jar:$KOTLIN_LIBS/kotlin-daemon-embeddable-$KOTLIN_VER.jar:$(ls "$KOTLIN_LIBS"/trove4j-*.jar):$(ls "$KOTLIN_LIBS"/annotations-*.jar | head -1):$(ls "$KOTLIN_LIBS"/kotlinx-coroutines-core-jvm-*.jar | head -1)"
STDLIB="$KOTLIN_LIBS/kotlin-stdlib-$KOTLIN_VER.jar"

echo "==> assets (translations and stock pictures are shared with the desktop client)"
SHARED="$ROOT/../DesktopClient/src/LanMessengerX.App/Assets"
mkdir -p "$OUT/assets/lang" "$OUT/assets/avatars"
cp "$SHARED"/lang/*.json "$OUT/assets/lang/"
cp "$SHARED"/avatars/[0-9]*.png "$OUT/assets/avatars/"

echo "==> resources (aapt)"
# The shared manifest carries no package attribute (Gradle/AGP takes it from "namespace"); aapt needs it.
sed "s#<manifest #<manifest package=\"$APP_ID\" #" "$ROOT/app/src/main/AndroidManifest.xml" > "$OUT/AndroidManifest.xml"
aapt package -f -m -J "$OUT/gen" -M "$OUT/AndroidManifest.xml" -S "$ROOT/app/src/main/res" \
  -A "$OUT/assets" -I "$ANDROID_JAR" --min-sdk-version $MIN_SDK --target-sdk-version $TARGET_SDK \
  --version-code "$versionCode" --version-name "$versionName" -F "$OUT/resources.apk"
javac -nowarn --release 17 -d "$OUT/rclasses" $(find "$OUT/gen" -name '*.java')

echo "==> Kotlin"
java -cp "$KCP" org.jetbrains.kotlin.cli.jvm.K2JVMCompiler -no-stdlib -no-reflect -jvm-target 17 -Xjvm-default=all \
  -cp "$STDLIB:$ANDROID_JAR:$OUT/rclasses" -d "$OUT/classes" \
  $(find "$ROOT/core/src/main" "$ROOT/app/src/main" -name '*.kt') 2>&1 | filt
[ -n "$(find "$OUT/classes" -name '*.class' | head -1)" ] || { echo "Kotlin compilation failed"; exit 1; }
(cd "$OUT/classes" && jar cf ../app-classes.jar .)
(cd "$OUT/rclasses" && jar cf ../r-classes.jar .)

echo "==> R8 (shrink)"
java -cp "$R8_JAR" com.android.tools.r8.R8 --release --min-api $MIN_SDK --lib "$ANDROID_JAR" --lib "$JAVA_HOME_DIR" \
  --pg-conf "$HERE/proguard-rules.pro" --output "$OUT/dex" \
  "$OUT/app-classes.jar" "$OUT/r-classes.jar" "$STDLIB" 2>&1 | filt
[ -f "$OUT/dex/classes.dex" ] || { echo "R8 failed"; exit 1; }

echo "==> package"
cp "$OUT/resources.apk" "$OUT/unsigned.apk"
(cd "$OUT/dex" && zip -q -j "$OUT/unsigned.apk" classes.dex)
zipalign -f -p 4 "$OUT/unsigned.apk" "$OUT/aligned.apk"

if [ ! -f "$KEYSTORE" ]; then
  echo "==> creating signing key $KEYSTORE"
  mkdir -p "$(dirname "$KEYSTORE")"
  keytool -genkeypair -keystore "$KEYSTORE" -storepass "$KS_PASS" -keypass "$KS_PASS" -alias "$KEY_ALIAS" \
    -keyalg RSA -keysize 2048 -validity 36500 -dname "CN=LAN Messenger X, O=LAN Messenger X" 2>&1 | filt
fi
FINAL="$OUT/LANMessengerX-$versionName.apk"
apksigner sign --ks "$KEYSTORE" --ks-pass "pass:$KS_PASS" --key-pass "pass:$KS_PASS" --ks-key-alias "$KEY_ALIAS" \
  --min-sdk-version $MIN_SDK --out "$FINAL" "$OUT/aligned.apk" 2>&1 | filt
apksigner verify --min-sdk-version $MIN_SDK "$FINAL" 2>&1 | filt
echo "==> $FINAL ($(du -h "$FINAL" | cut -f1))"
aapt dump badging "$FINAL" | grep -E "^(package|sdkVersion|targetSdkVersion|launchable-activity)"
