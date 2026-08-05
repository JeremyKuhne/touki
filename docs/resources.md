# Resources and Legacy Serialization

[`Touki.Resources`](../touki/Touki/Resources/) provides low-level access to
binary `.resources` files, inspection and opt-in deserialization of .NET
Remoting Binary Format (NRBF) payloads, and localized string lookup from loose
culture side files. These APIs are available on .NET 10 and .NET Framework
4.7.2.

## `RawResourceReader`

[`RawResourceReader`](../touki/Touki/Resources/RawResourceReader.cs) reads the
default version 2 binary `.resources` format without using `ResourceReader` to
deserialize stored values. It can locate entries by ordinal, case-sensitive
name; report their index, type code, and raw byte length; and copy names or
supported value bytes into caller-provided spans or streams.

```csharp
using Touki.Resources;

using RawResourceReader reader = RawResourceReader.CreateFromFile("My.resources");

if (reader.TryFindResource("Greeting", out ResourceLocation location))
{
    Console.WriteLine($"{location.TypeCode}: {location.ByteLength} bytes");
}
```

`CreateFromFile` memory-maps the file and transfers ownership of that mapping to
the reader, so dispose it. The `ReadOnlyMemory<byte>` constructor reads
caller-owned memory instead.

The reader exposes raw content for strings, primitives, byte arrays, and stream
entries. For serialized user types it exposes type metadata but not value bytes,
and it never instantiates the stored type. A bad `.resources` magic number
throws `ArgumentException`. A version other than 2 - including input truncated
before the version field - throws `NotSupportedException`. Other malformed or
truncated structures generally throw `BadImageFormatException`. The format
reader supports little-endian systems.

## Inspecting NRBF payloads

[`BinaryFormattedObject`](../touki/Touki/Resources/BinaryFormattedObject.cs)
parses an NRBF stream into `System.Formats.Nrbf` records. Construction does not
instantiate payload-defined types, and the supplied stream remains open.

```csharp
using Touki.Resources;

using FileStream stream = File.OpenRead("payload.bin");
BinaryFormattedObject payload = new(stream);

Console.WriteLine(payload.RootRecord);
Console.WriteLine($"{payload.RecordMap.Count} records");
```

Parsing is inspection-only and does not instantiate payload-defined types, but
deserialization must remain opt-in: call `Deserialize` only for trusted payloads
with an allowlisted resolver. Parsing still allocates record state, so callers
accepting untrusted input should impose an application-specific payload-size
limit.

Use `RootRecord`, `RecordMap`, and the record-id indexer when structural
inspection is enough.

### Deserializing trusted payloads

`Deserialize()` instantiates the parsed graph through an
[`ITypeResolver`](../touki/Touki/ITypeResolver.cs). The default
[`RegisteredTypeResolver`](../touki/Touki/RegisteredTypeResolver.cs) recognizes
a fixed framework type set; register each additional trusted type explicitly.

Deserialization can run serialization constructors, `ISerializable` code,
callbacks, and `IObjectReference` implementations. Register only trusted types
and call `Deserialize()` only for trusted payloads. It is a one-shot operation:
a second call throws even when the first call failed.

## `SatelliteStringResourceManager`

[`SatelliteStringResourceManager`](../touki/Touki/Resources/SatelliteStringResourceManager.cs)
extends `ResourceManager` with loose side-file probing. It looks under
`<probeRoot>/<culture>/<baseName>.resources`, merges specific and parent
cultures, and falls back to the assembly's embedded neutral resources.

```csharp
using System.Globalization;
using Touki.Resources;

SatelliteStringResourceManager resources = new(
    baseName: "MyApp.Resources.Strings",
    assembly: typeof(Program).Assembly);

string? greeting = resources.GetString("Greeting", new CultureInfo("de-DE"));
```

Only intrinsic string entries are loaded from side files. Other primitives,
arrays, streams, and serialized user types are skipped without deserialization.
Unreadable files, unsupported resource formats, and structurally malformed side
files are treated as absent. The two-argument constructor uses the `resources`
directory under `AppContext.BaseDirectory` as its probe root.
