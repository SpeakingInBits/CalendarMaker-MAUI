#!/usr/bin/env bash
# Builds the Windows app self-contained for each runtime and packages it with Velopack, so installed copies can update
# themselves from the GitHub releases.
#
#   VERSION=1.2.3 packaging/package.sh <rid>...
#
# Run from the repository root on Windows (Git Bash), with the maui-windows workload installed and vpk
# (dotnet tool install -g vpk) on the PATH at the same version as the Velopack package in CalendarMaker-MAUI.csproj.
# Each runtime is its own Velopack channel, named after the runtime, so an installed app only ever updates to its own
# platform's package. Writes to dist/:
#
#   CalendarMaker-<rid>-Setup.exe       installer (installs for the current user, no admin needed)
#   CalendarMaker-<rid>-Portable.zip    a folder that runs from anywhere and still updates
#   CalendarMaker-<version>-<rid>-full.nupkg, releases.<rid>.json   what installed apps download to update
set -euo pipefail

version=${VERSION:?set VERSION, e.g. VERSION=1.2.3}
mkdir -p dist
dist=$(cd dist && pwd)

for rid in "$@"; do
  out="out/$rid"
  rm -rf "$out"

  case "$rid" in
    win-*)
      # WinUI apps can't be published as a single file, so Velopack packages the whole folder. WindowsAppSDKSelfContained
      # puts the Windows App SDK in it too, so users don't need the Windows App Runtime installed. TargetFrameworks
      # leaves out the other platforms, whose restore would otherwise look for Windows runtime packs that don't exist.
      dotnet publish CalendarMaker-MAUI/CalendarMaker-MAUI.csproj \
        -p:TargetFrameworks=net10.0-windows10.0.19041.0 --framework net10.0-windows10.0.19041.0 \
        --configuration Release --runtime "$rid" --self-contained --output "$out/CalendarMaker" \
        -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -p:DebugType=none \
        -p:ApplicationDisplayVersion="$version" -p:Version="$version"

      # Some native packages ship their own debug symbols, which users don't need.
      find "$out/CalendarMaker" -name '*.pdb' -delete

      # No delta packages: each update downloads the whole app, which keeps the release simple.
      # The icon is Setup.exe's; the app's own icon is built into CalendarMaker-MAUI.exe.
      vpk pack --packId CalendarMaker --packTitle "Photo Calendar Maker" --packAuthors SpeakingInBits \
        --packVersion "$version" --channel "$rid" --runtime "$rid" --delta None --outputDir "$dist" \
        --packDir "$out/CalendarMaker" --mainExe CalendarMaker-MAUI.exe \
        --icon CalendarMaker-MAUI/Resources/AppIcon/photo_calendar_maker.ico
      ;;
    *)
      echo "Don't know how to package $rid. Only Windows is released so far." >&2
      exit 1
      ;;
  esac
done
