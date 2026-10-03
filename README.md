# Objo Plugin SDK 1.1.0

Download the `plugin-sdk-1.1.0.zip` release from
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
- `samples/`: database, DOCX, native image and shared-foundation wrappers.

Set `OBJO_PLUGIN_SDK_ROOT` to the extracted distribution directory. From a
copy of `template/FirstMaths`, run:

```sh
dotnet "$OBJO_PLUGIN_SDK_ROOT/tool/objo-plugin.dll" build plugin.json
dotnet "$OBJO_PLUGIN_SDK_ROOT/tool/objo-plugin.dll" inspect out/org.example.firstmaths-1.0.0.objopackage
```

`build` runs the wrapper build, generation, rebuild and packing in order,
streams each step's output, and stops with the step named if it fails. It uses
Release unless `plugin.json` sets `configuration`. Set `project` to a relative
`.csproj` path when needed; otherwise it finds the nearest directory containing
one `.csproj`, starting beside `assembly` and walking up to `plugin.json`.
Multiple projects in that directory require an explicit `project`. Keep
`assembly` and `xmlDocumentation` aligned with the chosen configuration.
The separate `generate` and `pack` commands remain available for custom workflows.

The `out/` package is the one file to give consumers. Replace the template
identity and licence before distributing a new plugin, and root the visible
namespace in your own brand or domain: the `Objo` namespace is reserved for
use by Objo Studio / Pettet Industries. `plugin.json` paths
are relative to that file and must stay within its directory. The supplied
native image sample currently declares macOS arm64 assets only; build and test
additional RIDs before adding them to its package input.

## Return a Picture from raw pixels

Returning `ObjoPictureFrame` publishes raw pixels straight to Objo's `Picture`
type with no image-file encode or decode step — the natural way to expose a
renderer, camera or emulator framebuffer. For tightly packed 8-bit RGB pixels,
use the convenience factory:

The factories below were added in SDK distribution 1.0.2. The SDK contract
version remains `1.0.0`.

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
them. This support is scheduled for Objo Studio 26.10.1; the current stable
26.9.4 release does not include it. Remote execution also needs a matching
Remote Debugger. Updating the SDK alone does not update Studio or application
hosts. For older compatible plugin runtimes, construct the descriptor, buffer
and frame explicitly instead of calling the factories.

## Where the documentation lives

The full beginner tutorial, consumer guide, recipes, the complete C#↔Objo
[value-mapping table](https://docs.objo.dev/#/plugin-sdk/overview), and API
reference are in the official Objo documentation's
**[Plugin SDK section](https://docs.objo.dev/#/plugin-sdk/overview)**.
