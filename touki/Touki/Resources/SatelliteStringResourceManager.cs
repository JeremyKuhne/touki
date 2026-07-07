// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
#if NET
using System.Collections.Frozen;
#endif

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
///   The merged table for the last requested culture is cached (as a frozen dictionary on .NET) and
///   rebuilt when the requested culture changes, which fits the overwhelmingly common
///   single-UI-culture usage. The cache is a single immutable snapshot published through a
///   <see langword="volatile"/> field, so it is updated without locking: under concurrent use with a
///   changing culture a lookup may briefly observe the snapshot for a previously requested culture (a
///   stale result), but never a torn or inconsistent one, and never a failure.
///  </para>
///  <para>
///   Only string resources are supported. Each entry's stored type is inspected with
///   <c>ResourceReader.GetResourceData</c>, which reads the raw type tag and bytes without
///   deserializing the value; only entries tagged as intrinsic strings are decoded, and every other
///   entry (a primitive, an array, or a serialized object) is skipped without deserialization. Files
///   that would require a non-default reader (for example one written by
///   <c>System.Resources.Extensions.PreserializedResourceWriter</c>) are rejected by the
///   <see cref="ResourceReader"/> constructor and treated as absent. No value is ever deserialized, so
///   no reflection or legacy serialization is triggered - keeping the manager trim- and AOT-safe and
///   never exposing untrusted <c>.resources</c> content to a deserializer.
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

#if NET
    private static readonly IReadOnlyDictionary<string, string> s_emptyStrings = FrozenDictionary<string, string>.Empty;
#else
    private static readonly IReadOnlyDictionary<string, string> s_emptyStrings =
        new Dictionary<string, string>(0, StringComparer.Ordinal);
#endif

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

    private IReadOnlyDictionary<string, string> BuildStrings(CultureInfo culture)
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

#if NET
        static FrozenDictionary<string, string> Freeze(Dictionary<string, string> table) =>
            table.ToFrozenDictionary(StringComparer.Ordinal);
#else
        static Dictionary<string, string> Freeze(Dictionary<string, string> table) => table;
#endif
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
            // default-format string .resources file. ResourceReader throws ArgumentException for a bad
            // magic number and NotSupportedException for a non-default reader type (for example one
            // written by PreserializedResourceWriter for resources that need reflection).
            // UnauthorizedAccessException covers a probe directory or file the process cannot read.
            // In every case, behave as if absent.
            return null;
        }
    }

    private static Dictionary<string, string> LoadStringTable(string path)
    {
        Dictionary<string, string> table = new(StringComparer.Ordinal);
        using ResourceReader reader = new(path);

        // Collect the names first. enumerator.Key returns the name without touching the value; reading
        // enumerator.Value, by contrast, would deserialize every entry - invoking reflection or legacy
        // serialization for non-string entries - which would break the trim/AOT guarantee and turn
        // untrusted .resources content into a deserialization surface.
        List<string> names = [];
        IDictionaryEnumerator enumerator = reader.GetEnumerator();
        while (enumerator.MoveNext())
        {
            if (enumerator.Key is string key)
            {
                names.Add(key);
            }
        }

        foreach (string name in names)
        {
            // GetResourceData reads the stored type tag and the raw value bytes without deserializing.
            reader.GetResourceData(name, out string resourceType, out byte[] resourceData);

            // Keep only intrinsic strings; every other entry (a primitive, an array, or a serialized
            // object) is skipped, so no value is ever deserialized.
            if (!string.Equals(resourceType, "ResourceTypeCode.String", StringComparison.Ordinal))
            {
                continue;
            }

            // A ResourceTypeCode.String payload is a length-prefixed UTF-8 string - exactly the format
            // BinaryReader.ReadString expects - so it decodes with no serializer or reflection.
            using MemoryStream stream = new(resourceData);
            using BinaryReader valueReader = new(stream, System.Text.Encoding.UTF8);
            table[name] = valueReader.ReadString();
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
        internal CultureCache(string cultureName, bool isNeutral, IReadOnlyDictionary<string, string>? strings)
        {
            CultureName = cultureName;
            IsNeutral = isNeutral;
            Strings = strings;
        }

        internal string CultureName { get; }

        internal bool IsNeutral { get; }

        internal IReadOnlyDictionary<string, string>? Strings { get; }
    }
}
