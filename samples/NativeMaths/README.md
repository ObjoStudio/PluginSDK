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

On Windows, build the DLL, add the produced asset to the `native` array in
`plugin.json`, then build the package. `build-native.ps1` needs `cl` on PATH,
so run it from the "x64 Native Tools Command Prompt for VS" or a Visual Studio
Developer PowerShell:

```powershell
.\build-native.ps1
dotnet "$env:OBJO_PLUGIN_SDK_ROOT\tool\objo-plugin.dll" build plugin.json
```

If Windows blocks a script downloaded in a ZIP, run
`powershell -ExecutionPolicy Bypass -File .\build-native.ps1`, or call
`Unblock-File .\build-native.ps1` once. The script only wraps a single `cl`
invocation, so any Windows C compiler produces the same DLL. The manual
equivalents (the directory does not ship in the distribution, hence the
`mkdir`) are:

```bat
mkdir native\win-x64
cl /nologo /O2 /W3 /LD /Inative\include /Fonative\win-x64\nativemaths.obj /Fenative\win-x64\acme_nativemaths.dll native\src\nativemaths.c
```

```bat
mkdir native\win-x64
gcc -O2 -shared -Inative\include -o native\win-x64\acme_nativemaths.dll native\src\nativemaths.c
```

The `gcc` form is MinGW; the header's `__declspec(dllexport)` exports the
function on Windows, so no export list is needed.

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
