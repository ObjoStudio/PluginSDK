# Objo Plugin SDK 1.0.0

Download the `plugin-sdk-1.0.0.zip` release from
[ObjoStudio/PluginSDK](https://github.com/ObjoStudio/PluginSDK/releases), then
extract it to a working folder. Authors need the .NET 10 SDK; plugin users need
only Objo Studio and a `.objopackage` file. The SDK binaries are covered by
`BINARY-LICENCE.txt`; the template and samples carry their own licences.

The distribution contains:

- `tool/objo-plugin.dll`: generator, package builder and inspector.
- `lib/Objo.Runtime.Abstractions.dll`: supported wrapper reference.
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

The full beginner tutorial, consumer guide, recipes, compatibility table and
API reference are in the official Objo documentation's **Plugin SDK** section.
