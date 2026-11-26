# Objo Plugin SDK 1.6.0

Download the `plugin-sdk-1.6.0.zip` release from
[ObjoStudio/PluginSDK](https://github.com/ObjoStudio/PluginSDK/releases), then
extract it to a working folder. Authors need the .NET 10 SDK; plugin users need
only Objo Studio and a `.objopackage` file. The SDK binaries are covered by
`BINARY-LICENCE.txt`; the template and samples carry their own licences.

The distribution contains:

- `tool/objo-plugin.dll`: source builder, generator, package builder and inspector.
- `lib/Objo.Runtime.Abstractions.dll` with its `.xml` IntelliSense
  documentation: the supported wrapper reference.
- `template/FirstMaths`: first plugin with methods, property, typed event,
  XML help, licence and package input.
- `samples/`: minimal native, database, DOCX, native image and shared-foundation wrappers.

## Ready-made sample plugins

The versioned [release downloads](https://github.com/ObjoStudio/PluginSDK/releases)
also offer the samples as individual `.objopackage` files. Import one with
**File > Plugin Library**, install it, then add it to your project. No C#
knowledge, .NET SDK or build step is needed. See the
[consumer guide](https://docs.objo.dev/#/plugin-sdk/recipes?id=ready-made-sample-plugins)
for installation and Objo usage.

The download listing records platforms, minimum Studio version and SHA-256
for each package. Documents and SharedFoundation are managed and portable;
Database includes SQLite native assets for macOS, Windows and Linux on arm64
and x64. NativeMaths and ImageEffects Tone currently support **macOS arm64 only**.
Install `acme.foundation` before Creator or Mutator, and `acme.image.foundation`
before Tone. Samples require Studio 26.10.1 or later and a matching Remote
Debugger for remote execution.

Prebuilt sample versions follow the SDK distribution version. Keep the exact
download: saved solutions pin both version and hashes. Release sources use
the same sample versions; the FirstMaths tutorial keeps version 1.0.0.

## Build a plugin from source

Set `OBJO_PLUGIN_SDK_ROOT` to the extracted distribution directory. From a
copy of `template/FirstMaths`, run:

```sh
export OBJO_PLUGIN_SDK_ROOT="$HOME/Downloads/plugin-sdk"
dotnet "$OBJO_PLUGIN_SDK_ROOT/tool/objo-plugin.dll" build plugin.json
dotnet "$OBJO_PLUGIN_SDK_ROOT/tool/objo-plugin.dll" inspect out/org.example.firstmaths-1.0.0.objopackage
```

On Windows PowerShell, set and read the variable through the `$env:` prefix; a
bare `$OBJO_PLUGIN_SDK_ROOT` names a PowerShell variable, not the environment
variable, and expands to nothing:

```powershell
$env:OBJO_PLUGIN_SDK_ROOT = 'C:\Users\you\Downloads\plugin-sdk'
dotnet "$env:OBJO_PLUGIN_SDK_ROOT\tool\objo-plugin.dll" build plugin.json
dotnet "$env:OBJO_PLUGIN_SDK_ROOT\tool\objo-plugin.dll" inspect out/org.example.firstmaths-1.0.0.objopackage
```

`build` runs the wrapper build, generation, rebuild and packing in order,
streams each step's output, and stops with the step named if it fails. It uses
Release unless `plugin.json` sets `configuration`. Set `project` to the wrapper's
`.csproj` path when needed; otherwise it finds the nearest directory containing
one `.csproj`, starting beside `assembly` and walking up to `plugin.json`.
Multiple projects in that directory require an explicit `project`. Keep
`assembly` and `xmlDocumentation` aligned with the chosen configuration.
An assembly outside the configuration directory requires an explicit `project`.
If the wrapper project is external, include the locally generated registration
file in its compilation.
The separate `generate` and `pack` commands remain available for custom workflows.

The produced `.objopackage` archive is the one file to give consumers. Replace
the template identity and licence before distributing a new plugin, and root the visible
namespace in your own brand or domain: the `Objo` namespace is reserved for
use by Objo Studio / Pettet Industries. Input paths in `plugin.json` may be
absolute or relative to that file, including `.` and `..` segments and symbolic
links. Native libraries, managed dependencies, data, licence files and dependency
archives can remain in shared directories outside the wrapper project. The SDK
copies their bytes to validated package paths, such as
`runtimes/win-x64/native/libWord.dll` for a Windows native library.

The archive `output` may be relative to `plugin.json`, including `.` and `..`
segments, or an absolute path in the host operating system's syntax. For example,
`"output": "../out/acme.graphics.png-1.0.0.objopackage"` writes to a
shared folder beside the plugin's directory. Both `build` and `pack` create any
missing output directories. Archive output paths must not pass through symbolic
links.

Generated `registration` and `apiMetadata` paths must stay beneath
`plugin.json`, using forward-slash relative paths without dot segments or
symbolic links. Package entry paths remain strictly validated. The supplied
native image sample currently declares macOS arm64 assets only; build and test
additional RIDs before adding them to its package input. The minimal
`samples/NativeMaths` sample ships the exercised `osx-arm64` asset and both a
`build-native.sh` and a `build-native.ps1`; build and add further RIDs before
declaring them.

## Return a Picture from raw pixels

Returning `ObjoPictureFrame` publishes raw pixels straight to Objo's `Picture`
type with no image-file encode or decode step — the natural way to expose a
renderer, camera or emulator framebuffer. For tightly packed 8-bit RGB pixels,
use the convenience factory:

The factories below were added in SDK distribution 1.0.2 with SDK contract
version `1.0.0`.

```csharp
using Objo.Runtime.Abstractions;

/// <summary>Renders one 160×144 RGB framebuffer.</summary>
[ObjoExport]
public sealed class Screen
{
    private readonly byte[] _pixels = new byte[160 * 144 * 3];

    /// <summary>Returns the current frame as a Picture.</summary>
    public ObjoPictureFrame FramePicture() =>
        ObjoPictureFrame.FromRgb(160, 144, _pixels);
}
```

`FromRgb`, `FromRgba` and `FromGrey` each own one immutable copy of the input
array, so you can reuse your framebuffer after the call. They declare top-down
sRGB pixels without row padding. `FromRgba` requires premultiplied alpha and
rejects colour components greater than alpha; convert straight alpha first.
The optional `scaleFactor` defaults to `1.0`; pass `2.0` for pixels rendered at
twice the logical resolution. Dispose the returned frame's `Image` when it is
no longer needed.

For other layouts, construct a descriptor, buffer and frame explicitly.
Picture export accepts 8-bit Grey, RGB or premultiplied RGBA, top-down or
bottom-up, without an ICC profile. Objo code calls `FramePicture()` and receives
an ordinary `Picture`, ready for a Canvas or ImageViewer.

### Runtime compatibility

Plugins calling these factories need a Studio/runtime build that includes
them: Objo Studio 26.10.1 or later. Remote execution also needs a matching
Remote Debugger. Updating the SDK alone does not update Studio or application
hosts. For older compatible plugin runtimes, construct the descriptor, buffer
and frame explicitly instead of calling the factories.

## Constants (SDK 1.5.0)

Public C# `const` fields on an `[ObjoExport]` class become typed, shared,
read-only Objo constants. The template's `Calculator.DEFAULT_START` is an
`Integer` constant. Numeric range endpoints can use ordinary Integer or Double
values, so a method can accept any intermediate value without an enum.
Boolean and non-null String constants are supported too. XML summaries and
`[ObjoDescription]` appear in Studio completion and hover help.

SDK distribution 1.5.0 implements contract 1.1.0. Constant-bearing APIs need a
Studio/runtime with contract 1.1.0 support; older 1.0.0 packages remain supported.
Checked reads embed the literal, and generated runtime getters return the same
value without running the wrapper's static initialiser. Updating the SDK alone
does not update Studio or the Remote Debugger. See the
[constant reference](https://docs.objo.dev/#/plugin-sdk/reference?id=constants)
for supported CLR types, limits and conditional-compilation examples.

## Use List<T> signatures

SDK distribution 1.6.0 maps supported C# `List<T>` parameters and results to
typed Objo arrays. The FirstMaths template's
`Calculator.RunningTotals(List<long> amounts)` takes `Integer()` and returns
`Integer()` in Objo. Paste these statements into a Command Line project's
Run event body.

```objo
Var calculator As New Example.Maths.Calculator(10)
Var totals() As Integer = calculator.RunningTotals([4, -1, 3])
Print(totals[0], totals[1], totals[2])
Return 0
```

This prints `14 13 16`. The bridge copies the collection at each crossing.
List-bearing packages require SDK contract 1.2.0 in Studio and the runtime.
See the [list reference](https://docs.objo.dev/#/plugin-sdk/reference?id=list-signatures).

## Where the documentation lives

The full beginner tutorial, consumer guide, recipes, the complete C#↔Objo
[value-mapping table](https://docs.objo.dev/#/plugin-sdk/overview), and API
reference are in the official Objo documentation's
**[Plugin SDK section](https://docs.objo.dev/#/plugin-sdk/overview)**.
