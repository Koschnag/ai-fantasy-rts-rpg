#!/usr/bin/env sh
set -eu

if [ "$(uname -s)" != "Linux" ] || [ ! -r /etc/os-release ]; then
  printf 'Dieses Skript ist nur für Ubuntu vorgesehen.\n' >&2
  exit 2
fi

. /etc/os-release

if [ "${ID:-}" != "ubuntu" ]; then
  printf 'Erkanntes System ist %s, nicht Ubuntu.\n' "${ID:-unbekannt}" >&2
  exit 2
fi

printf 'Installiere die FOSS-Entwicklungsbasis aus den Ubuntu-Repositories. sudo kann nach dem lokalen Passwort fragen.\n'
sudo apt-get update
sudo apt-get install --no-install-recommends \
  build-essential clang lld cmake ninja-build pkg-config zlib1g-dev \
  libsdl3-dev git-lfs ripgrep fd-find jq tree fzf just shellcheck shfmt \
  sqlite3 blender imagemagick glslang-tools spirv-tools

git lfs install --skip-repo
