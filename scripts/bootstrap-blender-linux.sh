#!/usr/bin/env sh
set -eu

rift_blender_version=5.2.0
rift_archive="blender-$rift_blender_version-linux-x64.tar.xz"
rift_sha256="96f6c181a30f4950607839dc84d42a354b250d8a0231b098b59b7bc69c351c48"

if [ "$(uname -s)/$(uname -m)" != "Linux/x86_64" ]; then
  printf 'Dieses Bootstrap-Skript unterstützt derzeit Linux x64.\n' >&2
  exit 2
fi

rift_opt_root=${RIFT_OPT_DIR:-"$HOME/.local/opt"}
rift_install_dir="$rift_opt_root/blender-$rift_blender_version"
rift_binary="$rift_install_dir/blender"

if [ -x "$rift_binary" ]; then
  mkdir -p "$HOME/.local/bin"
  ln -sfn "$rift_binary" "$HOME/.local/bin/blender"
  printf 'Blender %s ist bereits unter %s installiert.\n' "$rift_blender_version" "$rift_install_dir"
  exit 0
fi

rift_tmp=$(mktemp -d)
trap 'rm -rf -- "$rift_tmp"' EXIT HUP INT TERM
rift_download="$rift_tmp/$rift_archive"
rift_url="https://download.blender.org/release/Blender5.2/$rift_archive"

printf 'Lade %s ...\n' "$rift_url"
curl --proto '=https' --tlsv1.2 --fail --silent --show-error --location --output "$rift_download" "$rift_url"
printf '%s  %s\n' "$rift_sha256" "$rift_download" | sha256sum --check --status

mkdir -p "$rift_opt_root" "$HOME/.local/bin"
tar -xJf "$rift_download" -C "$rift_tmp"
mv "$rift_tmp/blender-$rift_blender_version-linux-x64" "$rift_install_dir"
ln -sfn "$rift_binary" "$HOME/.local/bin/blender"

"$rift_binary" --version | sed -n '1,2p'
