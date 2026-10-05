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
  output="${sample_root}/native/${rid}"
  mkdir -p "${output}"
  clang -O2 -std=c11 -fPIC -dynamiclib -arch "${architecture}" \
    -I "${sample_root}/native/include" \
    -o "${output}/libacme_nativemaths.dylib" "${sample_root}/native/src/nativemaths.c"
elif [[ "${system}" == "Linux" ]]; then
  case "${machine}" in
    x86_64) rid="linux-x64" ;;
    aarch64) rid="linux-arm64" ;;
    *) echo "Unsupported Linux architecture: ${machine}" >&2; exit 1 ;;
  esac
  output="${sample_root}/native/${rid}"
  mkdir -p "${output}"
  cc -O2 -std=c11 -fPIC -shared -I "${sample_root}/native/include" \
    -o "${output}/libacme_nativemaths.so" "${sample_root}/native/src/nativemaths.c"
else
  echo "On Windows, run build-native.ps1 from a Visual Studio x64 developer prompt." >&2
  exit 1
fi
echo "Built native maths asset for ${rid} in ${output}"
