#!/usr/bin/env sh
set -eu

rift_lfs_version=3.7.1
rift_archive="git-lfs-linux-amd64-v$rift_lfs_version.tar.gz"
rift_sha256="1c0b6ee5200ca708c5cebebb18fdeb0e1c98f1af5c1a9cba205a4c0ab5a5ec08"

if [ "$(uname -s)/$(uname -m)" != "Linux/x86_64" ]; then
  printf 'Dieses Bootstrap-Skript unterstützt derzeit Linux x64.\n' >&2
  exit 2
fi

if command -v git-lfs >/dev/null 2>&1 && [ "$(git-lfs version | awk '{print $1}')" = "git-lfs/$rift_lfs_version" ]; then
  git lfs install --skip-repo
  printf 'Git LFS %s ist bereits installiert.\n' "$rift_lfs_version"
  exit 0
fi

rift_tmp=$(mktemp -d)
trap 'rm -rf -- "$rift_tmp"' EXIT HUP INT TERM
rift_download="$rift_tmp/$rift_archive"
rift_url="https://github.com/git-lfs/git-lfs/releases/download/v$rift_lfs_version/$rift_archive"

printf 'Lade %s ...\n' "$rift_url"
curl --proto '=https' --tlsv1.2 --fail --silent --show-error --location --output "$rift_download" "$rift_url"
printf '%s  %s\n' "$rift_sha256" "$rift_download" | sha256sum --check --status
tar -xzf "$rift_download" -C "$rift_tmp"

rift_binary=$(find "$rift_tmp" -type f -name git-lfs -print -quit)
if [ -z "$rift_binary" ]; then
  printf 'Das geprüfte Archiv enthält kein git-lfs-Binary.\n' >&2
  exit 3
fi

mkdir -p "$HOME/.local/bin"
install -m 0755 "$rift_binary" "$HOME/.local/bin/git-lfs"
git lfs install --skip-repo
git lfs version
