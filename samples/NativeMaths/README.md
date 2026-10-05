# Acme native maths

The smallest native plugin sample: one C function behind one exported C# class.

- `native/include/acme/nativemaths.h`, `native/src/nativemaths.c` —
  `acme_nativemaths_add`, the one native function.
- `Adder.cs` — the exported `Acme.NativeMaths.Adder` wrapper that P/Invokes it.
- `plugin.json` — declares the exercised `osx-arm64` native asset.

A prebuilt `osx-arm64` library is included, so `build plugin.json` works on a
Mac before any native toolchain is installed. After editing the C source,
rebuild the library for your machine, then build and pack the package:

```sh
./build-native.sh
dotnet "$OBJO_PLUGIN_SDK_ROOT/tool/objo-plugin.dll" build plugin.json
```

On Windows, run `build-native.ps1` from the "x64 Native Tools Command Prompt
for VS" or a Visual Studio Developer PowerShell, add the built asset to the
`native` array in `plugin.json`, then build:

```powershell
.\build-native.ps1
dotnet "$env:OBJO_PLUGIN_SDK_ROOT\tool\objo-plugin.dll" build plugin.json
```

The line to add for Windows is:

```json
{ "rid": "win-x64", "file": "native/win-x64/acme_nativemaths.dll" }
```

Build and test every RID you declare before distributing a package, and run a
published app on each target; `build-native.sh` covers other macOS and Linux
targets the same way — build into `native/<rid>/` and add the path.

In Objo, the package exports `Acme.NativeMaths.Adder` with one method:

```objo
Var adder As New Acme.NativeMaths.Adder
Print("Sum", adder.Add(19, 23))
```
