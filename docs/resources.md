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
name, or a stream factory. Use `FromAssemblyFile` to parse a managed assembly as
data without loading it:

```csharp
StringResourceManager fromFile = new("Resources/Strings.resources");

StringResourceManager fromAssembly = new(
    "MyApp.Resources.Strings",
    typeof(Program).Assembly);

StringResourceManager fromAssemblyFile =
    StringResourceManager.FromAssemblyFile(
        "MyApp.Resources.Strings",
        "external/MyApp.dll");

StringResourceManager fromStream = new(
    "MyApp.Resources.Strings",
    () => File.OpenRead("Resources/Strings.resources"));
```

Construction does not open or parse the source. `FromAssemblyFile` captures a
fully qualified path during construction. The first lookup opens the source,
validates its type codes, binary-searches the existing resource index, and
decodes only the requested string. The indexed backing and last decoded string
remain cached; warmed lookup allocates nothing. Resource and managed assembly
files are memory-mapped, and loaded assemblies expose their manifest payload as
unmanaged memory, so none of these paths copies the complete resource image.
Other stream-factory sources must be readable and seekable. The backing is
retained until `ReleaseAllResources()` and then disposed.
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
    `<root>/<culture>/<assemblyName>.resources.dll` as data while neutral
    resources come from an already-loaded assembly; and
- `FromAssemblyFiles` parses neutral resources from a managed owner assembly
    file and localized resources from
    `<root>/<culture>/<ownerAssemblyName>.resources.dll`, without loading either
    assembly.

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

SatelliteStringResourceManager externalResources =
    SatelliteStringResourceManager.FromAssemblyFiles(
        "MyApp.Resources.Strings",
        "external/MyApp.dll",
        "external");

string? externalGreeting =
    externalResources.GetString("Greeting", new CultureInfo("de-DE"));
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

Direct assembly modes memory-map assemblies and inspect them as PE files; they
do not load or execute them. The owner assembly's
`NeutralResourcesLanguageAttribute` controls the neutral-culture boundary.
Assembly identity validation is opt-in. Pass
`StringResourceManagerOptions.ValidateAssemblyIdentity` to compare satellite
simple name, culture, version (including `SatelliteContractVersionAttribute`),
and public key token against the owner assembly.

Strict probing is the default: only an absent localized file continues parent
or neutral fallback, while unreadable, unsupported, malformed, or incorrectly
bundled candidates throw. When assembly identity validation is enabled, an
identity mismatch also throws. The culture chain is opened before key lookup,
so a malformed parent candidate throws even when a more-specific table contains
the key. Pass
`SatelliteStringResourceProbeMode.FallbackOnFailure` to the direct satellite or
assembly-files factory to treat all of those localized candidate failures as
missing and continue fallback. This mode never suppresses neutral owner or
neutral resource failures.

Before selecting an external-all layout, validate its neutral owner strictly
against the generated owner assembly:

```csharp
StringResourceManager.ValidateAssemblyFile(
    "MyApp.Resources.Strings",
    "external/MyApp.dll",
    typeof(Program).Assembly);
```

Calling `ValidateAssemblyFile` is itself an explicit opt-in. It parses the owner
as data, compares name, version, culture, and public key token, and validates the
complete neutral string table. Missing, unreadable, malformed, unsupported, or
identity-mismatched owners throw, allowing the host to choose its managed
fallback before committing to NativeAOT.

When the NativeAOT-generated owner has a different simple name from its managed
resource family, supply the external name explicitly. For example, a generated
`dotnet-aot` owner can consume the managed `dotnet` family without weakening the
remaining identity checks:

```csharp
StringResourceManager.ValidateAssemblyFile(
    baseName,
    "dotnet.dll",
    generatedOwnerAssembly,
    "dotnet");

StringResourceManager externalLocalized =
    SatelliteStringResourceManager.FromSatelliteDirectory(
        baseName,
        satelliteRoot,
        generatedOwnerAssembly,
        "dotnet",
        StringResourceManagerOptions.ValidateAssemblyIdentity,
        SatelliteStringResourceProbeMode.FallbackOnFailure);

StringResourceManager externalAll =
    SatelliteStringResourceManager.FromAssemblyFiles(
        baseName,
        "dotnet.dll",
        satelliteRoot,
        generatedOwnerAssembly,
        "dotnet",
        StringResourceManagerOptions.ValidateAssemblyIdentity,
        SatelliteStringResourceProbeMode.FallbackOnFailure);
```

The alias replaces only the expected owner/satellite simple name and satellite
filename. Version, culture, public-key token, neutral-language metadata, and
satellite contract version remain anchored to the generated owner assembly.
With `ValidateAssemblyIdentity` enabled, the aliased `FromAssemblyFiles`
overload performs strict neutral preflight before returning the manager and
compares the owner identity again each time the file is opened for a lazy
lookup or reopened after `ReleaseAllResources()`.

The end-to-end NativeAOT smoke test reads a generated accessor for ILC-embedded
French exact and German parent lookup, `FromSatelliteDirectory`,
`FromResourcesDirectory`, and `FromAssemblyFiles` exact, parent, and neutral
fallback using a managed owner assembly and satellite DLL from a separate
managed build. External layouts can
exclude localized `.resx` inputs from the native publish and deploy the managed
owner assembly plus its culture directories after publishing. Embedded mode
retains those publish inputs for ILC. Direct assembly DLLs and loose resource
files are trusted deployment artifacts. Automatic publish externalization
remains SDK/build tooling work.

## Generated string accessors

For the executive design, performance, and memory analysis, see
[Generated string resource solution](string-resource-manager-solution.md).

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
cache in constant time. A localized sibling causes the generated class to create
its manager through `StringResourceManagerProvider`. With no registration, the
provider preserves the managed default by calling
`SatelliteStringResourceManager.FromRuntimeSatellites`; exact, parent, missing,
and neutral fallback then follow the runtime-satellite manager.

Without a localized sibling, the accessor creates a `StringResourceManager`
directly by default. To deploy localized resources only as external files,
opt the neutral resource into the provider even when no localized `.resx`
files are included in the build:

```xml
<EmbeddedResource Update="Resources\Strings.resx"
                  Generator="Touki"
                  UseResourceManagerProvider="true" />
```

Register a provider at process startup before any generated localized accessor
is used to select an external NativeAOT layout:

```csharp
StringResourceManagerProvider.Register(
    (baseName, ownerAssembly) =>
        SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            externalRoot,
            ownerAssembly,
            SatelliteStringResourceProbeMode.FallbackOnFailure));
```

For NativeAOT resources embedded by ILC, register the platform resource adapter
instead:

```csharp
StringResourceManagerProvider.RegisterEmbedded();
```

`ResourceManagerAdapter` delegates lookup and release to
`System.Resources.ResourceManager`, allowing the NativeAOT resource
implementation to resolve embedded exact, parent, and neutral resources without
calling `Assembly.GetSatelliteAssembly`. The unregistered managed default
remains Touki's runtime-satellite manager. NativeAOT entry points must register
embedded or external behavior before any generated localized accessor is used.

Use `ValidateAssemblyFile` followed by `FromAssemblyFiles` in the provider when
neutral resources are external too. Tolerant satellite probing does not weaken
the strict neutral preflight.
Provider selection is process-wide and thread-safe. The first registration or
unregistered generated-manager creation freezes the selection; a later or
second registration throws. Each generated class uses a nested static holder,
so its provider is invoked exactly once even during concurrent first access.
Warmed generated property reads still return their cached field directly and do
not call the provider or manager.

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
