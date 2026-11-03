#!/usr/bin/env bash
# Writes a version's release notes (release-notes.md) to stdout, after checking their download links against the
# release's files, so renaming or adding a package can't leave users with a broken link or an installer they can't find.
#
#   VERSION=1.2.3 packaging/release-notes.sh <folder with the release's files>
#
# Fails when a link names a file that isn't in the release or another version, or an installer has no link.
set -euo pipefail
shopt -s nullglob

version=${VERSION:?set VERSION, e.g. VERSION=1.2.3}
dist=${1:?pass the folder with the files being released, e.g. dist}
notes=$(sed "s/{{VERSION}}/$version/g" "$(dirname "$0")/release-notes.md")
problems=0

# What follows releases/download/ in each link: v<version>/<file>.
links=$(grep -o 'releases/download/[^)]*' <<< "$notes" | sed 's|releases/download/||' | sort -u || true)
for link in $links; do
  file=$(basename "$link")
  if [[ $link != "v$version/$file" ]]; then
    echo "::error::release-notes.md links to $link, which isn't this version. Use v{{VERSION}} in the link." >&2
    problems=1
  elif [[ ! -f $dist/$file ]]; then
    echo "::error::release-notes.md links to $file, which isn't in this release." >&2
    problems=1
  fi
done

for installer in "$dist"/*-Setup.exe "$dist"/*-Portable.zip "$dist"/*.AppImage; do
  name=$(basename "$installer")
  if ! grep -qx "v$version/$name" <<< "$links"; then
    echo "::error::release-notes.md has no download link for $name." >&2
    problems=1
  fi
done

[[ $problems == 0 ]] || exit 1
echo "$notes"
