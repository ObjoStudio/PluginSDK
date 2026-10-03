# Objo Plugin SDK 1.0.1

Download the `plugin-sdk-1.0.1.zip` release from
[ObjoStudio/PluginSDK](https://github.com/ObjoStudio/PluginSDK/releases), then
extract it to a working folder. Authors need the .NET 10 SDK; plugin users need
only Objo Studio and a `.objopackage` file. The SDK binaries are covered by
`BINARY-LICENCE.txt`; the template and samples carry their own licences.

The distribution contains:

- `tool/objo-plugin.dll`: generator, package builder and inspector.
- `lib/Objo.Runtime.Abstractions.dll` with its `.xml` IntelliSense
  documentation: the supported wrapper reference.
- `template/FirstMaths`: first plugin with methods, property, typed event,
  XML help, licence and package input.
- `samples/`: database, DOCX, native image and shared-foundation wrappers.

Set `OBJO_PLUGIN_SDK_ROOT` to the extracted distribution directory. From a
copy of `template/FirstMaths`, run:

```sh
dotnet build FirstMaths.csproj -c Release
dotnet "$OBJO_PLUGIN_SDK_ROOT/tool/objo-plugin.dll" generate plugin.json
dotnet build FirstMaths.csproj -c Release
dotnet "$OBJO_PLUGIN_SDK_ROOT/tool/objo-plugin.dll" pack plugin.json
dotnet "$OBJO_PLUGIN_SDK_ROOT/tool/objo-plugin.dll" inspect out/org.example.firstmaths-1.0.0.objopackage
```

The `out/` package is the one file to give consumers. Replace the template
identity and licence before distributing a new plugin. `plugin.json` paths
are relative to that file and must stay within its directory. The supplied
native image sample currently declares macOS arm64 assets only; build and test
additional RIDs before adding them to its package input.

## Return a Picture from raw pixels

Returning `ObjoPictureFrame` publishes raw pixels straight to Objo's `Picture`
type with no image-file encode or decode step — the natural way to expose a
renderer, camera or emulator framebuffer. Describe the layout, wrap the bytes,
and pair the buffer with a scale factor (use `1` for unscaled pixels):

```csharp
using Objo.Runtime.Abstractions;

/// <summary>Renders one 160×144 RGB framebuffer.</summary>
[ObjoExport]
public sealed class Screen
{
    private readonly byte[] _pixels = new byte[160 * 144 * 3];

    /// <summary>Returns the current frame as a Picture.</summary>
    public ObjoPictureFrame FramePicture()
    {
        var descriptor = new ObjoImageDescriptor(160, 144, 160 * 3,
            ObjoPixelChannels.Rgb, 8, ObjoRowOrder.TopDown,
            ObjoAlphaMode.None, ObjoColourSpace.Srgb, mutable: false);
        return new ObjoPictureFrame(new ObjoImageBuffer(descriptor, _pixels), 1.0);
    }
}
```

Picture export accepts 8-bit Grey, RGB or premultiplied RGBA, top-down or
bottom-up, without an ICC profile. Objo code calls `FramePicture()` and receives
an ordinary `Picture`, ready for a Canvas or ImageViewer.

## Where the documentation lives

The full beginner tutorial, consumer guide, recipes, the complete C#↔Objo
[value-mapping table](https://docs.objo.dev/#/plugin-sdk/overview), and API
reference are in the official Objo documentation's
**[Plugin SDK section](https://docs.objo.dev/#/plugin-sdk/overview)**.
