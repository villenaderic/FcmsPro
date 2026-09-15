#!/usr/bin/env bash
# FCMS Pro - macOS .app bundle + .dmg builder
#
# Prerequisites:
#   - .NET 8 SDK
#   - create-dmg (brew install create-dmg)
#   - Run on macOS (or cross-compile from another OS with -r osx-x64/osx-arm64,
#     but .app bundling and DMG creation must happen on macOS)
#
# Usage: ./build-dmg.sh [x64|arm64]

set -euo pipefail

ARCH="${1:-arm64}"
RID="osx-${ARCH}"
APP_NAME="FCMS Pro"
BUNDLE_ID="com.fcmspro.desktop"
VERSION="1.1.0"
PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../src/FcmsPro.Avalonia" && pwd)"
BUILD_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/build"
APP_BUNDLE="${BUILD_DIR}/${APP_NAME}.app"

echo "==> Publishing self-contained ${RID} build..."
rm -rf "${BUILD_DIR}"
mkdir -p "${BUILD_DIR}"
dotnet publish "${PROJECT_DIR}/FcmsPro.Avalonia.csproj" \
  -c Release -r "${RID}" --self-contained true \
  -p:PublishSingleFile=true \
  -o "${BUILD_DIR}/publish"

echo "==> Assembling .app bundle..."
mkdir -p "${APP_BUNDLE}/Contents/MacOS" "${APP_BUNDLE}/Contents/Resources"

cat > "${APP_BUNDLE}/Contents/Info.plist" << PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>${APP_NAME}</string>
    <key>CFBundleDisplayName</key>
    <string>${APP_NAME}</string>
    <key>CFBundleIdentifier</key>
    <string>${BUNDLE_ID}</string>
    <key>CFBundleVersion</key>
    <string>${VERSION}</string>
    <key>CFBundleShortVersionString</key>
    <string>${VERSION}</string>
    <key>CFBundleExecutable</key>
    <string>FcmsPro</string>
    <key>CFBundleIconFile</key>
    <string>app.icns</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>LSMinimumSystemVersion</key>
    <string>11.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
PLIST

cp -R "${BUILD_DIR}/publish/"* "${APP_BUNDLE}/Contents/MacOS/"

# app.icns is generated from Assets/app-icon-source.png (see the project's
# Assets/ folder) - this copy step just brings it into the .app bundle.
ICON_SRC="${PROJECT_DIR}/Assets/app.icns"
if [ -f "${ICON_SRC}" ]; then
  cp "${ICON_SRC}" "${APP_BUNDLE}/Contents/Resources/app.icns"
else
  echo "WARNING: ${ICON_SRC} not found - app bundle will use the default icon."
fi

echo "==> Ad-hoc signing (replace with a real Developer ID for distribution outside your own machine)..."
codesign --force --deep --sign - "${APP_BUNDLE}"

echo "==> Creating .dmg..."
create-dmg \
  --volname "${APP_NAME}" \
  --window-size 500 300 \
  --icon-size 100 \
  --app-drop-link 380 150 \
  "${BUILD_DIR}/FcmsPro-${VERSION}-${ARCH}.dmg" \
  "${APP_BUNDLE}" || echo "create-dmg failed - .app bundle is still available at ${APP_BUNDLE}"

echo "==> Done. Output in ${BUILD_DIR}"
