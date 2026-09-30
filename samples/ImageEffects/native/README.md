# Acme native image effects

This sample contains an MIT-licensed C library (`acme_effects`) that depends on
another small library (`acme_core`). Package both libraries for each target RID.

- `include/acme/core.h`, `src/core.c` — `libacme_core`: the transitive native
  dependency (a 64-bit rotate primitive).
- `include/acme/effects.h`, `src/effects.c` — `libacme_effects`: the library the
  `Acme.Image.Tone` wrapper P/Invokes. It provides raw-image box blur and
  integer gain operations. Effects use owned source, target and optional
  mask buffers; a row callback reports progress and requests cancellation.
- `include/acme/codec.h`, `src/codec.c` — PNG/JPEG byte interchange in
  `libacme_effects`, used when a file or WorkerMessage needs encoded bytes.
  Raw effect chains use the image owner without encoding between filters.
- `vendor/stb_image.h`, `vendor/stb_image_write.h`, `vendor/LICENSE` — pinned
  from `nothings/stb` commit `2c980bb59875b0d32144a71867fbdebb2f77cd20`
  under its public-domain/MIT dual licence. These headers compile into the
  native library; no runtime library is fetched.

The raw-image operations support interleaved Grey, RGB and RGBA with 8- or
little-endian 16-bit components, arbitrary valid stride and either row order.
An optional 8-bit Grey mask blends each output pixel with the original.
Alpha is preserved; premultiplied colour channels are clamped to their pixel's
alpha after processing, while straight-alpha transparent colour is preserved.
In-place wrappers stage results and commit only after native success, so
cancellation or an error does not publish a partial image. ICC bytes and colour
interpretation are carried by the shared image owner, not interpreted by this
sample's numerical filters.

The codec accepts PNG and JPEG input without embedded ICC profiles. It keeps
16-bit PNG component precision and returns a top-down image with colour space
declared `Unspecified`; the caller must choose an interpretation. Encoding
requires explicitly declared sRGB, eight-bit components and, for RGBA, straight
alpha. JPEG encoding rejects alpha. Unsupported conversion is an error instead
of an implicit precision, alpha or profile change. The codec bounds decoded
allocation before invoking the third-party decoder.

The libraries are built position-independent. The macOS build uses
`@loader_path` install names and the Linux build uses `$ORIGIN` for its
transitive core dependency, so the staged pair resolves from the same per-RID
`native/` directory without a development-machine fallback. Windows builds
export the public C functions explicitly and link the effects DLL to the core
DLL's import library; both DLLs belong in the same `native/` directory.

## Building per RID

Configure and build separately on each target (or with a matching cross
compiler). The resulting `out/` directory contains the two shared libraries
to stage together. For example:

```sh
cmake -S native -B /tmp/acme-effects-build -DCMAKE_BUILD_TYPE=Release
cmake --build /tmp/acme-effects-build --config Release
```

From `samples/ImageEffects`, `build-native.sh` compiles both libraries on the
current macOS or Linux target. The equivalent macOS arm64 commands are:

```sh
clang -O2 -std=c11 -fPIC -dynamiclib -arch arm64 -I include \
  -install_name @loader_path/libacme_core.dylib \
  -o out/libacme_core.dylib src/core.c
clang -O2 -std=c11 -fPIC -dynamiclib -arch arm64 -I include -I vendor \
  -install_name @loader_path/libacme_effects.dylib \
  -o out/libacme_effects.dylib src/effects.c src/codec.c out/libacme_core.dylib
```

The supplied Tone package declaration lists macOS arm64 assets. To ship another
RID, add its two compiled libraries to `Tone/plugin.json`, pack again, and run
a standalone published app on that target. Cross compilation alone is
insufficient evidence.
