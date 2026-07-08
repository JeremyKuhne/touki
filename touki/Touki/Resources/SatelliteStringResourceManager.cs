// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text;

namespace Touki.Resources;

/// <summary>
///  A <see cref="ResourceManager"/> that resolves localized <b>string</b> resources from loose
///  per-culture <c>.resources</c> files at runtime, falling back to the embedded neutral resources.
///  Enables localization under Native AOT without embedding every language into the single native
///  binary.
/// </summary>
/// <remarks>
///  <para>
///   Native AOT bakes every satellite assembly into the native image at publish time; there is no
///   runtime probe for a <c>&lt;culture&gt;/&lt;name&gt;.resources.dll</c> next to the application.
///   This manager sidesteps that limitation by reading raw <c>.resources</c> files shipped as
///   ordinary content next to the application, so only the small neutral set needs to be embedded.
///  </para>
///  <para>
///   For a requested culture the manager walks from the most specific culture up to (but not
///   including) the invariant culture, merging the side files it finds into a single table - more
///   specific cultures win. When the requested culture is the assembly's neutral culture the side
///   files are skipped entirely, since the neutral resources are embedded. When no side file
///   supplies the key it defers to the embedded neutral resources through the base
///   <see cref="ResourceManager"/>.
///  </para>
///  <para>
///   The merged table for the last requested culture is cached (as a frozen dictionary) and
///   rebuilt when the requested culture changes, which fits the overwhelmingly common
///   single-UI-culture usage. The cache is a single immutable snapshot published through a
///   <see langword="volatile"/> field, so it is updated without locking: under concurrent use with a
///   changing culture a lookup may briefly observe the snapshot for a previously requested culture (a
///   stale result), but never a torn or inconsistent one, and never a failure.
///  </para>
///  <para>
///   Only string resources are supported. Each side file is read with <see cref="RawResourceReader"/>,
///   which parses the binary <c>.resources</c> structure directly and never materializes managed
///   values. Only entries whose stored type is the intrinsic string type are decoded; every other
///   entry - a primitive, an array, a <see cref="Stream"/>, or a serialized user type - is skipped by
///   its type code alone, so no value is ever deserialized. This holds even for files written by
///   <c>System.Resources.Extensions.PreserializedResourceWriter</c>: their plain-string entries are
///   read and their serialized user-type entries are skipped, with no reflection or legacy
///   serialization ever triggered - keeping the manager trim- and AOT-safe and never exposing
///   untrusted <c>.resources</c> content to a deserializer. Only default-format version 2
///   <c>.resources</c> files are read; any other file is treated as absent.
///  </para>
/// </remarks>
public sealed class SatelliteStringResourceManager : ResourceManager
{
    private readonly string _baseName;
    private readonly string _probeRoot;
    private readonly string? _neutralCultureName;

    // Single-culture cache published as one immutable snapshot (see CultureCache). It holds the last
    // requested culture, whether that culture is the neutral one, and its merged string table, and is
    // replaced wholesale whenever the culture changes. The field is volatile so a concurrent reader
    // always observes a fully-initialized snapshot - never a torn mix of fields. See GetString.
    private volatile CultureCache? _cache;

    // IDE0301 suggests a '[]' collection expression here, but FrozenDictionary is abstract and cannot
    // be constructed that way (CS0144); the cached empty singleton is correct and allocation-free.
#pragma warning disable IDE0301
    private static readonly FrozenDictionary<string, string> s_emptyStrings = FrozenDictionary<string, string>.Empty;
#pragma warning restore IDE0301

    /// <summary>
    ///  Initializes a new instance of the <see cref="SatelliteStringResourceManager"/> class that
    ///  probes for side files under <see cref="AppContext.BaseDirectory"/> in a <c>resources</c>
    ///  subdirectory.
    /// </summary>
    /// <inheritdoc cref="SatelliteStringResourceManager(string, Assembly, string)"/>
    public SatelliteStringResourceManager(string baseName, Assembly assembly)
        : this(baseName, assembly, Path.Join(AppContext.BaseDirectory, "resources"))
    {
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="SatelliteStringResourceManager"/> class.
    /// </summary>
    /// <param name="baseName">
    ///  The root name of the resources, without culture or extension - for example
    ///  <c>MyApp.Resources.SR</c>. Used both to locate the embedded neutral set and to name the
    ///  per-culture side files.
    /// </param>
    /// <param name="assembly">The assembly that carries the embedded neutral resources.</param>
    /// <param name="probeRoot">
    ///  The directory that contains <c>&lt;culture&gt;/&lt;baseName&gt;.resources</c> files.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///  <paramref name="baseName"/>, <paramref name="assembly"/>, or <paramref name="probeRoot"/> is
    ///  <see langword="null"/>.
    /// </exception>
    public SatelliteStringResourceManager(string baseName, Assembly assembly, string probeRoot)
        : base(baseName, assembly)
    {
        ArgumentNullException.ThrowIfNull(probeRoot);
        _baseName = baseName;
        _probeRoot = probeRoot;

        // The neutral (embedded) culture never has a side file; knowing it lets us skip probing when
        // it is the one requested. Absent attribute simply disables that shortcut.
        _neutralCultureName =
            (Attribute.GetCustomAttribute(assembly, typeof(NeutralResourcesLanguageAttribute)) as NeutralResourcesLanguageAttribute)
                ?.CultureName;
    }

    /// <summary>
    ///  The directory searched for per-culture side files.
    /// </summary>
    public string ProbeRoot => _probeRoot;

    /// <inheritdoc/>
    public override string? GetString(string name, CultureInfo? culture)
    {
        ArgumentNullException.ThrowIfNull(name);
        culture ??= CultureInfo.CurrentUICulture;

        // Read the published snapshot once. The volatile field gives this read acquire semantics, so a
        // non-null reference is always a fully-initialized CultureCache and every field read below
        // comes from that one snapshot - never a torn mix.
        CultureCache? cache = _cache;
        if (cache is null || !string.Equals(cache.CultureName, culture.Name, StringComparison.Ordinal))
        {
            // Rebuild for the new culture and publish the snapshot atomically (the volatile write has
            // release semantics). No lock is taken - under a concurrent culture change a lookup may
            // observe a stale (previous-culture) snapshot, but never a torn or inconsistent one, and
            // never fails. The neutral culture is matched ordinally; string.Equals is null-safe, so a
            // missing NeutralResourcesLanguage attribute (null _neutralCultureName) simply never
            // matches and side files are always consulted.
            bool isNeutral = string.Equals(culture.Name, _neutralCultureName, StringComparison.Ordinal);
            cache = new CultureCache(culture.Name, isNeutral, isNeutral ? null : BuildStrings(culture));
            _cache = cache;
        }

        // A neutral culture has its resources embedded (Strings is null); every other culture consults
        // its merged side-file table first.
        if (!cache.IsNeutral && cache.Strings is { } strings && strings.TryGetValue(name, out string? value))
        {
            return value;
        }

        // Fall back to the embedded neutral set. Passing the invariant culture avoids a satellite
        // grovel that would always fail (and is unsupported) under Native AOT.
        return base.GetString(name, CultureInfo.InvariantCulture);
    }

    private FrozenDictionary<string, string> BuildStrings(CultureInfo culture)
    {
        Dictionary<string, string>? merged = null;

        for (CultureInfo current = culture;
            !current.Equals(CultureInfo.InvariantCulture)
                && !string.Equals(current.Name, _neutralCultureName, StringComparison.Ordinal);
            current = current.Parent)
        {
            Dictionary<string, string>? table = LoadTable(current.Name);
            if (table is null)
            {
                continue;
            }

            if (merged is null)
            {
                merged = table;
                continue;
            }

            // Parent cultures supply only the keys the more specific cultures did not.
            foreach (KeyValuePair<string, string> entry in table)
            {
                if (!merged.ContainsKey(entry.Key))
                {
                    merged[entry.Key] = entry.Value;
                }
            }
        }

        return merged is null ? s_emptyStrings : Freeze(merged);

        static FrozenDictionary<string, string> Freeze(Dictionary<string, string> table) =>
            table.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private Dictionary<string, string>? LoadTable(string cultureName)
    {
        string path = Path.Join(_probeRoot, cultureName, $"{_baseName}.resources");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return LoadStringTable(path);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or FormatException
            or BadImageFormatException
            or ArgumentException
            or NotSupportedException)
        {
            // The side file is missing, unreadable (locked or access-denied), malformed, or not a
            // default-format version 2 .resources file. RawResourceReader throws ArgumentException for a
            // bad magic number, NotSupportedException for an unsupported format version, and
            // BadImageFormatException for a truncated or malformed file; IOException and
            // UnauthorizedAccessException cover a probe directory or file the process cannot open. In
            // every case, behave as if absent.
            return null;
        }
    }

    [SkipLocalsInit]
    private static Dictionary<string, string> LoadStringTable(string path)
    {
        Dictionary<string, string> table = new(StringComparer.Ordinal);
        using RawResourceReader reader = RawResourceReader.CreateFromFile(path);

        // Walk the entries by index. RawResourceReader parses the .resources structure in place and
        // never materializes a value, so inspecting each entry's type code and reading a string's raw
        // content bytes triggers no reflection or legacy serialization.
        int count = reader.ResourceCount;

        // Decode names and values through scratch buffers that start on the stack and spill to the
        // shared ArrayPool only when an unusually long entry overflows them.
        using BufferScope<char> nameBuffer = new(stackalloc char[256]);
        using BufferScope<byte> valueBuffer = new(stackalloc byte[512]);

        for (int i = 0; i < count; i++)
        {
            ResourceLocation location = reader.GetLocation(i);

            // Keep only intrinsic strings; every other entry - a primitive, an array, a stream, or
            // a serialized user type - is skipped by its type code alone, so no value is decoded.
            if (location.TypeCode != ResourceTypeCode.String)
            {
                continue;
            }

            // The name is UTF-16; grow the scratch buffer on the rare chance a name does not fit.
            int nameLength;
            while (!reader.TryGetResourceName(i, nameBuffer.AsSpan(), out nameLength))
            {
                nameBuffer.EnsureCapacity(nameBuffer.Length * 2);
            }

            // The value is the string's UTF-8 content bytes with the length prefix already stripped.
            valueBuffer.EnsureCapacity(location.ByteLength);
            reader.TryGetResourceData(i, valueBuffer.AsSpan(), out int valueLength);

            table[nameBuffer[..nameLength].ToString()] = Encoding.UTF8.GetString(valueBuffer[..valueLength]);
        }

        return table;
    }

    /// <summary>
    ///  An immutable snapshot of the resolution state for a single culture. It is published atomically
    ///  through the <see langword="volatile"/> <c>_cache</c> field, so a reader that reads the
    ///  reference sees a fully-initialized instance - the culture name, the neutral flag, and the table
    ///  are always mutually consistent (never torn), even on weak memory models.
    /// </summary>
    private sealed class CultureCache
    {
        internal CultureCache(string cultureName, bool isNeutral, FrozenDictionary<string, string>? strings)
        {
            CultureName = cultureName;
            IsNeutral = isNeutral;
            Strings = strings;
        }

        internal string CultureName { get; }

        internal bool IsNeutral { get; }

        internal FrozenDictionary<string, string>? Strings { get; }
    }
}
