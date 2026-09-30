#!/usr/bin/env bash
set -euo pipefail

sample_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
machine="$(uname -m)"
system="$(uname -s)"
if [[ "${system}" == "Darwin" ]]; then
  case "${machine}" in
    arm64) rid="osx-arm64"; architecture="arm64" ;;
    x86_64) rid="osx-x64"; architecture="x86_64" ;;
    *) echo "Unsupported macOS architecture: ${machine}" >&2; exit 1 ;;
  esac
  output="${sample_root}/Tone/native/${rid}"
  mkdir -p "${output}"
  clang -O2 -std=c11 -fPIC -dynamiclib -arch "${architecture}" \
    -I "${sample_root}/native/include" \
    -install_name @loader_path/libacme_core.dylib \
    -o "${output}/libacme_core.dylib" "${sample_root}/native/src/core.c"
  clang -O2 -std=c11 -fPIC -dynamiclib -arch "${architecture}" \
    -I "${sample_root}/native/include" -I "${sample_root}/native/vendor" \
    -install_name @loader_path/libacme_effects.dylib \
    -o "${output}/libacme_effects.dylib" \
    "${sample_root}/native/src/effects.c" "${sample_root}/native/src/codec.c" \
    "${output}/libacme_core.dylib"
elif [[ "${system}" == "Linux" ]]; then
  case "${machine}" in
    x86_64) rid="linux-x64" ;;
    aarch64) rid="linux-arm64" ;;
    *) echo "Unsupported Linux architecture: ${machine}" >&2; exit 1 ;;
  esac
  output="${sample_root}/Tone/native/${rid}"
  mkdir -p "${output}"
  cc -O2 -std=c11 -fPIC -shared -I "${sample_root}/native/include" \
    -o "${output}/libacme_core.so" "${sample_root}/native/src/core.c"
  cc -O2 -std=c11 -fPIC -shared -Wl,-rpath,'$ORIGIN' \
    -I "${sample_root}/native/include" -I "${sample_root}/native/vendor" \
    -o "${output}/libacme_effects.so" \
    "${sample_root}/native/src/effects.c" "${sample_root}/native/src/codec.c" \
    -L "${output}" -lacme_core
else
  echo "Use a Windows native compiler for Windows target assets." >&2
  exit 1
fi
echo "Built native image assets for ${rid} in ${output}"
