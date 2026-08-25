#!/usr/bin/env bash
#
# T-010 nativer Build für linux-x64 (SDL3, bgfx/bx/bimg, bgfx-Shim, Shader).
#
# Verträge:
# - Pins ausschließlich aus toolchain.lock.json ("nativeComponents").
# - Erstbeschaffung ist protokolliert und hashverifiziert; danach genügt der
#   lokale Cache außerhalb von Git (.ai/runtime/cache/native) ohne Netzwerk.
# - Wiederholter Build wird gegen die aufgezeichneten Artefakthashes geprüft;
#   eine Hashabweichung schlägt fehl (--verify-cache prüft nur).
# - ISA-Vertrag: keine ISA oberhalb der konservativen x86-64-Basis
#   (-march=x86-64, kein -march=native, kein AVX2/FMA).
# - Keine Distributionspakete als Shipping-Version.
#
# Aufruf:
#   scripts/native-build-linux-x64.sh                # bauen (Cache-first) + Hashprüfung
#   scripts/native-build-linux-x64.sh --verify-cache # nur Hashprüfung, kein Netz/Build
#   scripts/native-build-linux-x64.sh --fresh        # Build erzwingen (Cache bleibt Quelle)
set -euo pipefail

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
root=$(dirname -- "$script_dir")
cd "$root"

mode=build
case "${1:-}" in
  ''|build) mode=build ;;
  --verify-cache) mode=verify ;;
  --fresh) mode=fresh ;;
  *)
    printf 'Unbekannter Modus: %s\n' "${1:-}" >&2
    exit 2
    ;;
esac

cache=.ai/runtime/cache/native
src=$cache/src
dist=$cache/dist
logs=$cache/logs
lock=toolchain.lock.json
artifact_hashes=$cache/artifact-hashes.json

mkdir -p "$src" "$dist/lib" "$dist/shaders" "$logs"

log() { printf '[native-build] %s\n' "$*"; }
die() { printf '[native-build] FEHLER: %s\n' "$*" >&2; exit 1; }

command -v jq >/dev/null 2>&1 || die "jq fehlt."
sha256bin() { sha256sum "$1" | cut -d' ' -f1; }

# Reproduzierbarkeit: bx.cpp bettet __DATE__/__TIME__ ein; SOURCE_DATE_EPOCH
# fixiert beide Makros (1786623387 == toolchain.lock.json generatedAtUtc
# 2026-08-13T12:16:27Z). Ohne diese Fixierung ist libbx.a nicht byte-stabil.
export SOURCE_DATE_EPOCH="${SOURCE_DATE_EPOCH:-1786623387}"

if [ "$mode" = verify ] && [ ! -s "$artifact_hashes" ]; then
  die "Modus --verify-cache: Artefakthash-Manifest $artifact_hashes fehlt; zuerst einen vollen Build ausführen."
fi

# ---------------------------------------------------------------- Toolchain
if [ -f "$cache/toolchain/env.sh" ]; then
  # Benutzerlokale Toolchain (sudo-freie Sitzung); liegt im Git-ignorierten Cache.
  # shellcheck disable=SC1091
  . "$cache/toolchain/env.sh"
fi
for t in gcc g++ cmake ninja make; do
  command -v "$t" >/dev/null 2>&1 || die "$t fehlt im PATH (Systempakete siehe docs/NATIVE_UNTERBAU.md)."
done

# Entwicklungsbaseline-Header/-Bibliotheken (X11/xkbcommon/EGL) liegen als
# Distributionsextraktion im Cache und sind kein Shipping-Artefakt
# (docs/NATIVE_UNTERBAU.md). CPATH/LIBRARY_PATH machen sie frueh gebunden,
# damit auch die genie-generierten bgfx-Makefiles sie finden.
toolchain_usr=$cache/toolchain/usr
if [ -d "$toolchain_usr/include" ]; then
  export CPATH="${CPATH:+$CPATH:}$toolchain_usr/include"
  export LIBRARY_PATH="${LIBRARY_PATH:+$LIBRARY_PATH:}$toolchain_usr/lib/x86_64-linux-gnu"
fi

# ------------------------------------------------------- Pins verifizieren
log 'Verifiziere Quell-Pins gegen toolchain.lock.json.'
ids=$(jq -r '.nativeComponents[].id' "$lock")
[ "$(printf '%s\n' $ids | sort -u | wc -l)" -eq 4 ] || die "toolchain.lock.json muss genau 4 nativeComponents enthalten."

pin_field() {
  jq -r --arg id "$1" --arg field "$2" \
    '.nativeComponents[] | select(.id == $id) | .[$field]' "$lock"
}

declare -A ARCHIVE
for id in sdl3 bgfx bx bimg; do
  sha=$(pin_field "$id" sourceSha256)
  case "$sha" in
    [0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f]*) : ;;
    *) die "Pin '$id': sourceSha256 fehlt oder ist malformed." ;;
  esac

  file=$(ls "$src"/${id}-*.tar.gz 2>/dev/null | head -1 || true)
  if [ -z "$file" ]; then
    if [ "$mode" = verify ]; then
      die "Modus --verify-cache: Quellarchiv für '$id' fehlt im Cache."
    fi
    url=$(pin_field "$id" sourceArchiveUrl)
    log "Lade gepinnte Quelle '$id' erstmalig: $url"
    file="$src/${id}-$(pin_field "$id" commit).tar.gz"
    curl -fsSL -o "$file" "$url" || die "Download von '$id' fehlgeschlagen."
  fi

  actual=$(sha256bin "$file")
  if [ "$actual" != "$sha" ]; then
    die "Hashabweichung bei '$id': erwartet $sha, erhalten $actual."
  fi
  ARCHIVE[$id]=$file
  log "Pin '$id': Hash OK ($actual)."
done

# ------------------------------------------------------------- Extraktion
EXTRACT_DIR=
extract() {
  local id=$1 first_entry
  # Ersten Archivpfad ohne Pipeline-SIGPIPE-Falle lesen (set -o pipefail).
  IFS= read -r first_entry < <(tar -tzf "${ARCHIVE[$id]}")
  [ -n "$first_entry" ] || die "Archiv '$id' ist leer oder unlesbar."
  EXTRACT_DIR=$src/"${first_entry%%/*}"
  if [ ! -d "$EXTRACT_DIR" ]; then
    log "Extrahiere $id."
    tar -xzf "${ARCHIVE[$id]}" -C "$src"
  fi
}
extract sdl3;  sdl_dir=$EXTRACT_DIR
extract bgfx;  bgfx_dir=$EXTRACT_DIR
extract bx;    bx_dir=$EXTRACT_DIR
extract bimg;  bimg_dir=$EXTRACT_DIR
log "Quellbäume: sdl3=$(basename "$sdl_dir") bgfx=$(basename "$bgfx_dir") bx=$(basename "$bx_dir") bimg=$(basename "$bimg_dir")"

if [ "$mode" = verify ]; then
  log '--verify-cache: überspringe Builds.'
else
  # ------------------------------------------------------------ SDL3 (CMake)
  if [ ! -f "$dist/lib/libSDL3.so.0" ] || [ "$mode" = fresh ]; then
    log 'Baue SDL3 (Release, minimale Optionen).'
    sdl_build=$cache/build/sdl3
    rm -rf "$sdl_build"
    mkdir -p "$sdl_build"
    cmake -S "$sdl_dir" -B "$sdl_build" -G Ninja \
      -DCMAKE_BUILD_TYPE=Release \
      -DCMAKE_C_COMPILER=gcc -DCMAKE_CXX_COMPILER=g++ \
      "-DCMAKE_C_FLAGS=-march=x86-64 -mtune=generic -idirafter $toolchain_usr/include" \
      "-DCMAKE_CXX_FLAGS=-march=x86-64 -mtune=generic -idirafter $toolchain_usr/include" \
      "-DCMAKE_PREFIX_PATH=$toolchain_usr" \
      "-DCMAKE_LIBRARY_PATH=$toolchain_usr/lib/x86_64-linux-gnu" \
      -DCMAKE_INSTALL_PREFIX="$dist" \
      -DBUILD_SHARED_LIBS=ON \
      -DSDL_STATIC=OFF -DSDL_SHARED=ON \
      -DSDL_TEST_LIBRARY=OFF -DSDL_TESTS=OFF -DSDL_EXAMPLES=OFF \
      -DSDL_DOCS=OFF \
      -DSDL_AUDIO=OFF -DSDL_JOYSTICK=OFF -DSDL_HAPTIC=OFF \
      -DSDL_SENSOR=OFF -DSDL_CAMERA=OFF -DSDL_DIALOG=OFF \
      -DSDL_GPU=OFF -DSDL_RENDER=OFF \
      -DSDL_WAYLAND=OFF -DSDL_X11=ON -DSDL_KMSDRM=OFF -DSDL_LIBUDEV=OFF \
      >"$logs/sdl3-configure.log" 2>&1 || { tail -30 "$logs/sdl3-configure.log" >&2; die "SDL3-CMake fehlgeschlagen."; }
    cmake --build "$sdl_build" -j"$(nproc)" >"$logs/sdl3-build.log" 2>&1 \
      || { tail -30 "$logs/sdl3-build.log" >&2; die "SDL3-Build fehlgeschlagen."; }
    cmake --install "$sdl_build" >"$logs/sdl3-install.log" 2>&1 \
      || { tail -30 "$logs/sdl3-install.log" >&2; die "SDL3-Install fehlgeschlagen."; }
  else
    log 'SDL3-Artefakte vorhanden; Build übersprungen (Cache).'
  fi

  # ------------------------------------------- bgfx-Familie (genie/gmake)
  genie=$bx_dir/tools/bin/linux/genie
  [ -x "$genie" ] || die "genie fehlt im gepinnten bx-Stand."
  # Absolute Form, damit der Aufruf nach 'cd "$bgfx_dir"' weiterhin aufloest.
  genie=$(CDPATH= cd -- "$(dirname -- "$genie")" && pwd)/$(basename -- "$genie")
  bgfx_dir=$(CDPATH= cd -- "$bgfx_dir" && pwd)
  # GENie erzeugt In-Tree-Projekte unter <bgfx>/.build/projects/gmake-linux-gcc.
  bgfx_projects_make=$bgfx_dir/.build/projects/gmake-linux-gcc
  if [ ! -f "$bgfx_projects_make/bgfx.make" ] || [ ! -f "$bgfx_projects_make/shaderc.make" ] || [ "$mode" = fresh ]; then
    log 'Generiere bgfx-Projekte (gmake/linux-gcc, ohne Beispiele).'
    rm -rf "$bgfx_dir/.build"
    mkdir -p "$bgfx_dir/.build"
    # GENie erwartet die geschwisterlichen Quellbaeume 'bx' und 'bimg' neben bgfx.
    ln -sfn "$(basename "$bx_dir")"   "$src/bx"
    ln -sfn "$(basename "$bimg_dir")" "$src/bimg"
    (cd "$bgfx_dir" && "$genie" --with-tools --gcc=linux-gcc gmake) \
      >"$logs/bgfx-genie.log" 2>&1 \
      || { tail -30 "$logs/bgfx-genie.log" >&2; die "genie-Generierung fehlgeschlagen."; }
    # OpenGL-3.3-Pflichtpfad: Renderer-Version am gepinnten Stand explizit auf 33 fixieren.
    # (config.h respektiert -DBGFX_CONFIG_RENDERER_OPENGL und schaltet damit den
    #  Core-Profil-Pfad ab Version >= 31 frei; siehe Pin-Begruendung in toolchain.lock.json.)
    sed -i 's/^\([[:space:]]*DEFINES[[:space:]]*+=.*\)$/\1 -DBGFX_CONFIG_RENDERER_OPENGL=33/' \
      "$bgfx_projects_make"/bgfx.make \
      || die "GL-3.3-Define konnte nicht in bgfx.make injiziert werden."
    grep -q '\-DBGFX_CONFIG_RENDERER_OPENGL=33' \
      "$bgfx_projects_make"/bgfx.make \
      || die "GL-3.3-Define fehlt in bgfx.make."
    # ISA-Vertrag: Die konservative Basis ist x86-64-v2 (SSE4.2/POPCNT, siehe
    # toolchain.lock.json nativeComponentsNote und docs/PLATTFORMMATRIX.md).
    # Upstream-bx deklariert am gepinnten Stand SSE4.2 als Mindestspezifikation
    # (include/bx/simd_t.h, "minspec is SSE4.2"); ohne -msse4.2 verweigern die
    # SSE4.1-/SSE4.2-Intrinsics in bx/bimg/bgfx die Kompilation. Die Flag wird
    # deshalb als dokumentierte Mindestbasis ueber die CFLAGS-/CXXFLAGS-Variablen
    # der generierten Makefiles gesetzt; -fPIC ermoeglicht das Binden der
    # statischen Archive in libriftbgfx.so (ohne PIC kollidiert local-exec-TLS
    # mit -shared). Streng verboten bleiben -march=native sowie jede
    # AVX-/AVX2-/FMA-Pflicht (wird unten geprueft).
  else
    log 'bgfx-Projektgenerierung vorhanden (Cache).'
  fi

  log 'Baue bgfx, bx, bimg und shaderc (release64).'
  make -R -j"$(nproc)" -C "$bgfx_projects_make" config=release64 \
    'CFLAGS=-msse4.2 -fPIC' 'CXXFLAGS=-msse4.2 -fPIC' \
    bx bimg bgfx shaderc >"$logs/bgfx-build.log" 2>&1 \
    || { tail -40 "$logs/bgfx-build.log" >&2; die "bgfx-Build fehlgeschlagen."; }

  # GENie release64 legt Artefakte in .build/linux64_gcc/bin ab (lib*Release.a,
  # Werkzeuge ohne Suffix-Erweiterung); siehe generierte bgfx.make TARGET-Zeilen.
  bgfx_bin=$bgfx_dir/.build/linux64_gcc/bin
  for lib in libbxRelease.a libbimgRelease.a libbgfxRelease.a; do
    [ -f "$bgfx_bin/$lib" ] || die "Statische Bibliothek fehlt: $lib"
  done
  [ -x "$bgfx_bin/shadercRelease" ] || die "shaderc wurde nicht gebaut."

  # ------------------------------------------------------ Shim (libriftbgfx)
  # Eingabehash ueber Shim-Quellen und Pins: verhindert stale Artefakte bei
  # inkrementellen Sitzungen (Quelländerung erzwingt Neubau); bei unveraenderten
  # Eingaben bleibt der Neubau unter fixiertem SOURCE_DATE_EPOCH byteidentisch.
  shim_input_stamp="$dist/lib/libriftbgfx.inputs.sha256"
  shim_input_hash=$(sha256sum \
    src/Riftward.Native/riftbgfx_shim.cpp \
    src/Riftward.Native/riftbgfx_shim.h \
    "$lock" 2>/dev/null | sha256sum | cut -d' ' -f1)
  shim_needs_build=0

  if [ ! -f "$dist/lib/libriftbgfx.so" ] || [ "$mode" = fresh ]; then
    shim_needs_build=1
  elif [ ! -f "$shim_input_stamp" ] || [ "$(cut -d' ' -f1 "$shim_input_stamp" 2>/dev/null)" != "$shim_input_hash" ]; then
    log 'Shim-Eingaben geaendert; baue libriftbgfx.so neu.'
    shim_needs_build=1
  fi

  if [ "$shim_needs_build" = 1 ]; then
    log 'Baue eigenen bgfx-Shim (libriftbgfx.so).'
    # Desktop-GL-Soname direkt binden (-l:<datei> findet die Laufzeitbibliothek
    # auch ohne Entwicklerpaket): liefert glGetString fuer die Diagnoseabfrage
    # und erzeugt einen DT_NEEDED-Eintrag; EGL/GLX oeffnet bgfx zur Laufzeit
    # selbst per dlopen.
    g++ -std=c++17 -O2 -fPIC -shared -Wall -Wextra -Werror \
      -march=x86-64-v2 -mtune=generic -fno-exceptions -fno-rtti \
      "-idirafter $toolchain_usr/include" \
      -I"$bgfx_dir/include" -I"$bx_dir/include" -I"$bimg_dir/include" \
      src/Riftward.Native/riftbgfx_shim.cpp \
      "$bgfx_bin/libbgfxRelease.a" "$bgfx_bin/libbimgRelease.a" "$bgfx_bin/libbxRelease.a" \
      -lpthread -ldl \
      -l:libGL.so.1 \
      -o "$dist/lib/libriftbgfx.so" \
      >"$logs/shim-build.log" 2>&1 \
      || { tail -40 "$logs/shim-build.log" >&2; die "Shim-Build fehlgeschlagen."; }
    printf '%s  libriftbgfx.inputs\n' "$shim_input_hash" >"$shim_input_stamp"
  else
    log 'Shim-Artefakt vorhanden (Cache, Eingabehash unveraendert).'
  fi

  # ------------------------------------------- Shader offline kompilieren
  # Alle Shader werden offline mit dem gepinnten shaderc fuer GLSL 130
  # uebersetzt; es findet keine Shaderkompilierung zur Laufzeit statt.
  # Einzelne Artefakte werden nur bei Fehlen bzw. --fresh neu uebersetzt;
  # der Reproduzierbarkeitsvertrag gilt unveraendert (byteidentische
  # Neubauten unter fixiertem SOURCE_DATE_EPOCH).
  compile_shader() {
    id=$1; type=$2; source=$3; varyingdef=${4:-}
    if [ "$mode" = fresh ] || [ ! -s "$dist/shaders/$id.bin" ]; then
      log "Kompiliere Shader '$source' ($type) offline (shaderc -> GLSL 130)."
      extra=""
      if [ -n "$varyingdef" ]; then
        extra="--varyingdef src/Riftward.Shaders/$varyingdef"
      fi
      # shellcheck disable=SC2086
      "$bgfx_bin/shadercRelease" --platform linux -p 130 --type "$type" \
        -i "$bgfx_dir/include" -i "$bgfx_dir/src" \
        -i src/Riftward.Shaders \
        $extra \
        -f "src/Riftward.Shaders/$source" \
        -o "$dist/shaders/$id.bin" >>"$logs/shaderc.log" 2>&1 \
        || { tail -20 "$logs/shaderc.log" >&2; die "Shaderkompilierung fehlgeschlagen: $source"; }
    else
      log "Shaderartefakt vorhanden (Cache): $id.bin"
    fi
  }

  compile_shader triangle.vs vertex triangle.vs.sc
  compile_shader triangle.fs fragment triangle.fs.sc
  compile_shader bench_empty.vs vertex bench_empty.vs.sc

  # T-023: Graybox-Belastungsframe (Terrain, Einheiten mit 48-Bone-Palette,
  # Schattenpaesse, Partikel). fs_lit wird je Programm mit passendem
  # varying.def kompiliert und erzeugt zwei Zieldateien.
  compile_shader terrain.vs vertex vs_terrain.sc lit_varying.def.sc
  compile_shader lit_terrain.fs fragment fs_lit.sc lit_varying.def.sc
  compile_shader unit.vs vertex vs_unit.sc unit_varying.def.sc
  compile_shader lit_unit.fs fragment fs_lit.sc unit_varying.def.sc
  compile_shader depth_static.vs vertex vs_depth_static.sc depth_varying.def.sc
  compile_shader depth_instanced.vs vertex vs_depth_instanced.sc depth_instanced_varying.def.sc
  compile_shader depth.fs fragment fs_depth.sc depth_instanced_varying.def.sc
  compile_shader particle.vs vertex vs_particle.sc particle_varying.def.sc
  compile_shader particle.fs fragment fs_particle.sc particle_varying.def.sc
fi

# ----------------------------------------------------- ISA-/Flag-Nachweis
log 'Prüfe generierte bgfx-Makefiles auf ISA-Anhebung über x86-64-v2 hinaus.'
# -msse4.2 entspricht der dokumentierten x86-64-v2-Basis (POPCNT/SSE4.2) und ist
# zulässig; verboten sind -march=native sowie jede AVX-/AVX2-/FMA-Pflicht.
if grep -RInE -- '-march=native|-mavx([^.0-9]|$)|-mavx2|-mavx512|-mfma' \
    "$bgfx_dir"/.build/projects/gmake-linux-gcc/*.make 2>/dev/null; then
  die "ISA-anhebende Compilerflags (native/AVX/AVX2/FMA) in generierten Makefiles gefunden."
fi

# ------------------------------------------------------------ Artefakthashes
# Manifest nur schreiben, wenn neue Artefakte entstanden sind oder noch keines
# existiert. In den Modi build (Cache-Treffer) und --verify-cache bleibt das
# aufgezeichnete Manifest bindend: Abweichende/beschaedigte Artefakte muessen
# die Prüfung unten fehlschlagen lassen und duerfen nicht still ueberschrieben
# werden.
if [ "$mode" = fresh ] || [ ! -s "$artifact_hashes" ]; then
  log 'Schreibe Artefakthash-Manifest.'
  {
    printf '{\n'
    first=1
    while IFS= read -r artifact; do
      rel=${artifact#./}
      hash=$(sha256bin "$artifact")
      size=$(wc -c <"$artifact" | tr -d ' ')
      [ "$first" -eq 1 ] || printf ',\n'
      first=0
      printf '  "%s": {"sha256": "%s", "bytes": %s}' "$rel" "$hash" "$size"
    done < <(find "$dist/lib" "$dist/shaders" -maxdepth 1 -type f \
      \( -name 'libSDL3.so.*' -o -name 'libriftbgfx.so' -o -name '*.bin' \) | LC_ALL=C sort)
    printf '\n}\n'
  } >"$artifact_hashes.tmp"
  mv "$artifact_hashes.tmp" "$artifact_hashes"
  log "Artefakthashes aufgezeichnet: $artifact_hashes"
else
  log 'Pruefe vorhandene Artefakte gegen aufgezeichnetes Hashmanifest (kein Neuschreiben).'
fi

# ---------------------------------------------------- Wiederholbarkeit prüfen
fail=0
while IFS='=' read -r rel expected; do
  if [ ! -f "$rel" ]; then
    printf '[native-build] FEHLER: Artefakt fehlt: %s\n' "$rel" >&2
    fail=$((fail + 1))
    continue
  fi
  actual=$(sha256bin "$rel")
  if [ "$actual" != "$expected" ]; then
    printf '[native-build] FEHLER: Hashabweichung bei %s\n' "$rel" >&2
    fail=$((fail + 1))
  fi
done < <(jq -r 'to_entries[] | "\(.key)=\(.value.sha256)"' "$artifact_hashes")
[ "$fail" -eq 0 ] || die "$fail Artefaktprüfung(en) fehlgeschlagen."

log 'ERGEBNIS: PASS (Quellenpins, Artefakte, ISA-Scan, Hashmanifest).'
