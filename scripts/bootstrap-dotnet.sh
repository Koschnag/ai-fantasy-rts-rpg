#!/usr/bin/env sh
set -eu

rift_sdk_version=10.0.110
rift_kernel=$(uname -s)
rift_machine=$(uname -m)

case "$rift_kernel/$rift_machine" in
  Linux/x86_64)
    rift_archive="dotnet-sdk-$rift_sdk_version-linux-x64.tar.gz"
    rift_sha512="05e5a22cef9f41748bbd63602a6b595322b91214d03b9a00da43d698501648136f1fb2a4fe6ce6ad9c684aa1698376821c8753af0c42e336b8f753f6c078fb28"
    ;;
  Linux/aarch64|Linux/arm64)
    rift_archive="dotnet-sdk-$rift_sdk_version-linux-arm64.tar.gz"
    rift_sha512="0d3bd6ef343dfbccdb065c3f12484a67af30772fd20b83f5a5267764acafcc67cc78297c3f54fd047b899075f1798ad269e3e4e864d9c0b7abd9f67fa9a43694"
    ;;
  Darwin/x86_64)
    rift_archive="dotnet-sdk-$rift_sdk_version-osx-x64.tar.gz"
    rift_sha512="6b41416fdb2569fe34b3ba5db43aebbc9544e57ad4ad2d1fa57dfc69b380a4a9f4f0e1d8ebcd515f52848518663434bee0eefd4e3c7477498a01790ecd99f9ed"
    ;;
  Darwin/arm64)
    rift_archive="dotnet-sdk-$rift_sdk_version-osx-arm64.tar.gz"
    rift_sha512="a02fe7ab4251b9a94e5373772f9982f2cfe5b1e91acd33079ad4f11ae286a202b5d632e5f39a530d62345aa920514f152a31c7aff21df230663f58381bac0bb1"
    ;;
  *)
    printf 'Nicht unterstützte Bootstrap-Plattform: %s/%s\n' "$rift_kernel" "$rift_machine" >&2
    exit 2
    ;;
esac

rift_data_root=${XDG_DATA_HOME:-"$HOME/.local/share"}
rift_install_dir=${RIFT_DOTNET_DIR:-"$rift_data_root/dotnet"}
rift_dotnet="$rift_install_dir/dotnet"

rift_ensure_dotnet_link() {
  # A correct pre-existing link already satisfies the bootstrap contract.
  # Do not replace it: the toolchain may intentionally be mounted read-only
  # inside CI or an agent sandbox.
  if [ -L "$1" ] \
    && [ "$(readlink "$1" 2>/dev/null || printf '')" = "$2" ]; then
    return 0
  fi

  if [ -e "$1" ] && [ ! -L "$1" ]; then
    printf '%s existiert und ist kein Symlink; Bootstrap abgebrochen.\n' "$1" >&2
    return 1
  fi

  ln -sfn "$2" "$1"
}

if [ -x "$rift_dotnet" ] && [ "$($rift_dotnet --version)" = "$rift_sdk_version" ]; then
  mkdir -p "$HOME/.local/bin"
  rift_link="$HOME/.local/bin/dotnet"
  rift_ensure_dotnet_link "$rift_link" "$rift_dotnet"
  printf '.NET SDK %s ist bereits unter %s installiert.\n' "$rift_sdk_version" "$rift_install_dir"
  exit 0
fi

rift_tmp=$(mktemp -d)
trap 'rm -rf -- "$rift_tmp"' EXIT HUP INT TERM
rift_download="$rift_tmp/$rift_archive"
rift_url="https://builds.dotnet.microsoft.com/dotnet/Sdk/$rift_sdk_version/$rift_archive"

printf 'Lade %s ...\n' "$rift_url"
curl --proto '=https' --tlsv1.2 --fail --silent --show-error --location --output "$rift_download" "$rift_url"

if command -v sha512sum >/dev/null 2>&1; then
  rift_actual=$(sha512sum "$rift_download" | awk '{print $1}')
elif command -v shasum >/dev/null 2>&1; then
  rift_actual=$(shasum -a 512 "$rift_download" | awk '{print $1}')
else
  printf 'Weder sha512sum noch shasum ist verfügbar. Installation abgebrochen.\n' >&2
  exit 3
fi

if [ "$rift_actual" != "$rift_sha512" ]; then
  printf 'SHA-512-Prüfung fehlgeschlagen für %s.\n' "$rift_archive" >&2
  exit 4
fi

mkdir -p "$rift_install_dir" "$HOME/.local/bin"
tar -xzf "$rift_download" -C "$rift_install_dir"

rift_link="$HOME/.local/bin/dotnet"
rift_ensure_dotnet_link "$rift_link" "$rift_dotnet"

"$rift_dotnet" --version
