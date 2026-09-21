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

## `StringResourceTableLoader`

[`StringResourceTableLoader`](../touki/Touki/Resources/StringResourceTableLoader.cs)
turns an in-memory `.resources` file or an embedded `.resources` payload inside
a managed assembly into a mutable, ordinal `Dictionary<string, string>`. It
accepts only intrinsic strings by default. Pass
`StringResourceManagerOptions.IgnoreNonStringResources` to skip other resource
types without retaining their names or deserializing their values. A null
resource always rejects the table.

```csharp
using Touki.Resources;

ReadOnlyMemory<byte> resources = await File.ReadAllBytesAsync("Strings.resources");
Dictionary<string, string> table =
    StringResourceTableLoader.LoadStringTableFromResourcesFile(resources);

ReadOnlyMemory<byte> satellite = await File.ReadAllBytesAsync("de/MyApp.resources.dll");
Dictionary<string, string>? localized =
    StringResourceTableLoader.LoadStringTableFromAssembly(
        satellite,
        "MyApp.Resources.Strings.de.resources");
```

`LoadStringTableFromAssembly` returns `null` when the named manifest resource is
absent. Resource data is expected to be a trusted output of the application's
build and deployment pipeline. Present malformed or unsupported data throws.
Loaded-assembly paths parse the original backing directly rather than copying
the complete stream. The returned dictionary materializes its keys and values.

## `StringResourceManager`

[`StringResourceManager`](../touki/Touki/Resources/StringResourceManager.cs)
provides `ResourceManager`-style string lookup over exactly one resource table.
Construct it with a `.resources` file path, an already-loaded assembly and base
name, or a stream factory:

```csharp
StringResourceManager fromFile = new("Resources/Strings.resources");

StringResourceManager fromAssembly = new(
    "MyApp.Resources.Strings",
    typeof(Program).Assembly);

StringResourceManager fromStream = new(
    "MyApp.Resources.Strings",
    () => File.OpenRead("Resources/Strings.resources"));
```

Construction does not open or parse the source. The first lookup opens the
source, validates its type codes, binary-searches the existing resource index,
and decodes only the requested string. The indexed backing and last decoded
string remain cached; warmed lookup allocates nothing. Files are memory-mapped,
and loaded assemblies expose their manifest payload as unmanaged memory, so
neither path copies the complete resource image. Other stream-factory sources
must be readable and seekable. The stream is retained until
`ReleaseAllResources()` and then disposed.
An abandoned file manager releases its mapped view through the backing's
finalizable lease; assembly and stream tables do not pay that finalization cost.

By default a null or non-string entry rejects the table. Pass
`StringResourceManagerOptions.IgnoreNonStringResources` to skip non-string
entries; skipped names behave as missing. Null entries remain invalid. Resource
data is expected to be trusted, compiler-produced application data. The manager
does not convert bad resource compilation or deployment into a missing result.
The manager represents one table, so the culture passed to `GetString` is
ignored. Assigning `IgnoreCase` throws `NotSupportedException`.

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
derives from `StringResourceManager` and adds culture fallback. Each instance
uses exactly one localized source selected by its factory:

- `FromRuntimeSatellites` uses normal runtime satellite assembly binding;
- `FromResourcesDirectory` reads
    `<root>/<culture>/<baseName>.resources`; and
- `FromSatelliteDirectory` parses
    `<root>/<culture>/<assemblyName>.resources.dll` as data.

Localized source modes are not mixed. Lookup walks from the requested culture
through its parents, then falls back to the neutral manager.

```csharp
using System.Globalization;
using Touki.Resources;

Assembly assembly = typeof(Program).Assembly;
SatelliteStringResourceManager resources =
    SatelliteStringResourceManager.FromRuntimeSatellites(
        "MyApp.Resources.Strings",
        assembly);

string? greeting = resources.GetString("Greeting", new CultureInfo("de-DE"));
```

Use an overload that accepts `StringResourceManager` to supply neutral resources
from a file or stream. The supplied manager remains caller-owned, so releasing
the satellite manager does not clear its cache.

Null resources always reject a localized table. By default non-string resources
also reject it. `IgnoreNonStringResources` skips non-string names and values, so
a matching name behaves as missing and normal culture fallback continues.
Resource names are matched ordinally and case-sensitively; assigning
`IgnoreCase` throws `NotSupportedException`. Localized tables, runtime satellite
binds, assembly metadata, and missing candidates are cached. Each file is opened
at most once per cache generation, including during concurrent first lookup.
`ReleaseAllResources()` starts a new table/file generation. Runtime assembly
metadata and successful or missing satellite bind results remain cached per
resource assembly for the process lifetime, matching the normal bundled-resource
deployment model.

Directory factories capture a fully qualified root during construction and
require the resource base name, culture name, and satellite assembly name to be
single path segments. These checks preserve deterministic probe layout and make
relative roots independent of later current-directory changes.

Direct satellite mode memory-maps assemblies and inspects them as PE files; it
does not load or execute them. Only an absent file or runtime satellite continues
fallback. A present unreadable file, unsupported format, malformed payload, or
assembly missing the expected manifest resource throws.

## Generated string accessors

`KlutzyNinja.Touki` ships a C# source generator for strongly typed string
resources. Select it per neutral resource item:

```xml
<ItemGroup>
    <EmbeddedResource Update="Resources\Strings.resx" Generator="Touki" />
</ItemGroup>
```

The generated static partial class exposes `ResourceManager`, `Culture`, and one
non-null string property per supported entry. Each property stores its first
resolved value in a generated field. Setting `Culture` replaces the complete
cache in constant time. A localized sibling causes the generated class to use
`SatelliteStringResourceManager.FromRuntimeSatellites`; exact, parent, missing,
and neutral fallback then follow the runtime-satellite manager.

When `Culture` is `null`, the first property read uses
`CultureInfo.CurrentUICulture` and remains cached until the `Culture` setter is
called. Calling `ResourceManager.ReleaseAllResources()` does not clear generated
property fields. These are deliberate warmed-lookup optimizations.

The generator retains `GenerateSource`, `ClassName`, `ManifestResourceName`,
`Link`/`RelativeDir`, `WithCulture`, `Public`, `IncludeDefaultValues`, and
`EmitFormatMethods`. `OmitGetResourceString`, `AsConstants`, and `NoWarn` are not
supported and produce `TOUKIRESX0002`. Typed entries produce `TOUKIRESX0001` and
are skipped while string entries continue generating. Generated-member conflicts
produce `TOUKIRESX0004`, and multiple selected resources targeting the same class
produce `TOUKIRESX0005`.

Generation is bounded to 8 MiB of RESX source, 4,096 entries, and 64 format
arguments per entry. Exceeding a bound produces `TOUKIRESX0003` instead of
emitting unbounded source into the compiler host.
