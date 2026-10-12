#!/usr/bin/env bash
# Builds release packages of the desktop client (Linux/macOS shell; Windows users: publish-windows.bat).
#   ./publish.sh                 -> linux-x64 (tar.gz + .deb) and win-x64 (zip)
#   ./publish.sh linux-arm64     -> any .NET runtime identifier(s)
# Output goes to DesktopClient/dist/. Needs the .NET 8 SDK; NuGet access is only needed for the first restore.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
VERSION="$(sed -n 's#.*<Version>\(.*\)</Version>.*#\1#p' src/LanMessengerX.App/LanMessengerX.App.csproj | head -1)"
RIDS=("$@"); [ ${#RIDS[@]} -gt 0 ] || RIDS=(linux-x64 win-x64)
DIST="$PWD/dist"; mkdir -p "$DIST"

for rid in "${RIDS[@]}"; do
  echo "==> publishing $rid"
  OUT="$DIST/build/$rid"; rm -rf "$OUT"
  dotnet publish src/LanMessengerX.App -c Release -r "$rid" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true \
    -p:DebugType=None -p:DebugSymbols=false -o "$OUT" >/dev/null
  NAME="LANMessengerX-$VERSION-$rid"
  case "$rid" in
    win-*)
      (cd "$OUT" && rm -f "$DIST/$NAME.zip" && zip -qr "$DIST/$NAME.zip" .)
      echo "    $DIST/$NAME.zip"
      ;;
    linux-*)
      tar -C "$OUT" --transform "s,^\./,$NAME/," -czf "$DIST/$NAME.tar.gz" .
      echo "    $DIST/$NAME.tar.gz"
      if command -v dpkg-deb >/dev/null; then
        arch=amd64; [ "$rid" = linux-arm64 ] && arch=arm64
        PKG="$DIST/build/deb-$rid"; rm -rf "$PKG"
        mkdir -p "$PKG/DEBIAN" "$PKG/opt/lanmessengerx" "$PKG/usr/bin" "$PKG/usr/share/applications" "$PKG/usr/share/icons/hicolor/256x256/apps"
        cp "$OUT/LanMessengerX" "$PKG/opt/lanmessengerx/LanMessengerX"
        ln -s /opt/lanmessengerx/LanMessengerX "$PKG/usr/bin/lanmessengerx"
        cp packaging/linux/lanmessengerx.desktop "$PKG/usr/share/applications/"
        cp packaging/linux/lanmessengerx.png "$PKG/usr/share/icons/hicolor/256x256/apps/"
        cat > "$PKG/DEBIAN/control" <<CTRL
Package: lanmessengerx
Version: $VERSION
Section: net
Priority: optional
Architecture: $arch
Maintainer: galandras12 <noreply@users.noreply.github.com>
Depends: libfontconfig1, libx11-6, libice6, libsm6
Recommends: libnotify-bin
Homepage: https://github.com/galandras12/LAN_MessengerX
Description: Serverless chat and file transfer for the local network
 LAN Messenger X speaks the original LAN Messenger protocol, so it works with the Android,
 Windows, Linux and older clients on the same network. No server, no account.
CTRL
        dpkg-deb --root-owner-group --build "$PKG" "$DIST/lanmessengerx_${VERSION}_$arch.deb" >/dev/null
        echo "    $DIST/lanmessengerx_${VERSION}_$arch.deb"
      fi
      ;;
  esac
done
