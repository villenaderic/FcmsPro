#!/usr/bin/env bash
# Stamps one version number into every file that hardcodes it, so the tag is the
# single source of truth. Usage: bash .github/set-version.sh 1.6.6
set -euo pipefail
V="${1:?usage: set-version.sh X.Y.Z}"
[[ "$V" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "Tag must look like vX.Y.Z (got '$V')"; exit 1; }
cd "$(dirname "${BASH_SOURCE[0]}")/.."

stamp() { # file, sed-expression
  sed -i.bak -E "$2" "$1"
  rm -f "$1.bak"
  grep -q "$V" "$1" || { echo "Failed to stamp $V into $1"; exit 1; }
  echo "stamped $1"
}

stamp src/FcmsPro.Avalonia/FcmsPro.Avalonia.csproj "s|<Version>[^<]*</Version>|<Version>${V}</Version>|"
stamp installers/windows/fcmspro.iss               "s|^#define MyAppVersion \"[^\"]*\"|#define MyAppVersion \"${V}\"|"
stamp installers/macos/build-dmg.sh                "s|^VERSION=\"[^\"]*\"|VERSION=\"${V}\"|"
stamp installers/linux/build-deb.sh                "s|^VERSION=\"[^\"]*\"|VERSION=\"${V}\"|"
stamp installers/linux/build-appimage.sh           "s|^VERSION=\"[^\"]*\"|VERSION=\"${V}\"|"
stamp installers/linux/debian/DEBIAN/control       "s|^Version: [0-9.]+|Version: ${V}|"
