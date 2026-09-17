#!/usr/bin/env bash
# FCMS Pro - Linux AppImage builder
#
# Prerequisites:
#   - .NET 8 SDK
#   - appimagetool (https://github.com/AppImage/AppImageKit/releases) on PATH
#     or downloaded to ./appimagetool-x86_64.AppImage in this folder
#
# Usage: ./build-appimage.sh

set -euo pipefail

APP_NAME="FcmsPro"
VERSION="1.1.1"
RID="linux-x64"
PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../src/FcmsPro.Avalonia" && pwd)"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="${SCRIPT_DIR}/build"
APPDIR="${BUILD_DIR}/${APP_NAME}.AppDir"

echo "==> Publishing self-contained ${RID} build..."
rm -rf "${BUILD_DIR}"
mkdir -p "${BUILD_DIR}"
dotnet publish "${PROJECT_DIR}/FcmsPro.Avalonia.csproj" \
  -c Release -r "${RID}" --self-contained true \
  -p:PublishSingleFile=true \
  -o "${BUILD_DIR}/publish"

echo "==> Assembling AppDir..."
mkdir -p "${APPDIR}/usr/bin" "${APPDIR}/usr/share/applications" "${APPDIR}/usr/share/icons/hicolor/256x256/apps"

cp -R "${BUILD_DIR}/publish/"* "${APPDIR}/usr/bin/"
chmod +x "${APPDIR}/usr/bin/${APP_NAME}"

cat > "${APPDIR}/usr/share/applications/fcmspro.desktop" << DESKTOP
[Desktop Entry]
Name=FCMS Pro
Exec=${APP_NAME}
Icon=fcmspro
Type=Application
Categories=Office;Finance;
DESKTOP

# app-256.png is generated from Assets/app-icon-source.png.
ICON_SRC="${PROJECT_DIR}/Assets/app-256.png"
if [ -f "${ICON_SRC}" ]; then
  cp "${ICON_SRC}" "${APPDIR}/usr/share/icons/hicolor/256x256/apps/fcmspro.png"
  cp "${ICON_SRC}" "${APPDIR}/fcmspro.png"
else
  echo "WARNING: ${ICON_SRC} not found - AppImage will use a generic icon."
fi

# AppRun entry point required by the AppImage format.
cat > "${APPDIR}/AppRun" << 'APPRUN'
#!/usr/bin/env bash
HERE="$(dirname "$(readlink -f "${0}")")"
exec "${HERE}/usr/bin/FcmsPro" "$@"
APPRUN
chmod +x "${APPDIR}/AppRun"

cp "${APPDIR}/usr/share/applications/fcmspro.desktop" "${APPDIR}/fcmspro.desktop"

echo "==> Building AppImage..."
APPIMAGETOOL="appimagetool"
if ! command -v appimagetool &> /dev/null; then
  if [ -f "${SCRIPT_DIR}/appimagetool-x86_64.AppImage" ]; then
    APPIMAGETOOL="${SCRIPT_DIR}/appimagetool-x86_64.AppImage"
  else
    echo "ERROR: appimagetool not found on PATH or in ${SCRIPT_DIR}."
    echo "Download it from https://github.com/AppImage/AppImageKit/releases"
    exit 1
  fi
fi

# --appimage-extract-and-run avoids needing FUSE to run appimagetool itself
# (appimagetool is distributed as an AppImage, which normally self-mounts via
# FUSE to execute) - GitHub's standard ubuntu-latest runners don't have FUSE
# available, so a plain invocation fails with "dlopen(): error loading
# libfuse.so.2" before it ever gets to building anything. This flag makes it
# extract itself to a temp dir and run from there instead, which needs no
# special runner configuration.
"${APPIMAGETOOL}" --appimage-extract-and-run "${APPDIR}" "${BUILD_DIR}/FcmsPro-${VERSION}-x86_64.AppImage"

echo "==> Done. Output: ${BUILD_DIR}/FcmsPro-${VERSION}-x86_64.AppImage"
