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
///   single-UI-culture usage. The cache is updated without locking: under concurrent use with a
///   changing culture a lookup may briefly observe a table built for a different culture (a stale
///   result), but never an inconsistent one or a failure.
///  </para>
///  <para>
///   Only string resources are supported. A <c>.resources</c> file that
///   <see cref="ResourceReader"/> can open is always in the default binary format, whose non-string
///   entries are intrinsic types (<see langword="int"/>, <c>byte[]</c>, and so on) that read without
///   reflection and are ignored here. Files that would require reflection or serialization to read
///   (a non-default reader type, such as one written by
///   <c>System.Resources.Extensions.PreserializedResourceWriter</c>) are rejected by the
///   <see cref="ResourceReader"/> constructor and treated as absent, so no reflection or
///   serialization is ever triggered - keeping the manager trim- and AOT-safe.
///  </para>
/// </remarks>
public sealed class SatelliteStringResourceManager : ResourceManager
{
    private readonly string _baseName;
    private readonly string _probeRoot;
    private readonly string? _neutralCultureName;

    // Single-culture cache: the last requested culture, whether it is the neutral culture, and its
    // merged string table (null when the culture is neutral - its resources are embedded, so no side
    // files are consulted). Reset whenever the culture changes. Updated without locking; see GetString.
    private string? _cachedCultureName;
    private bool _cachedCultureIsNeutral;
    private IReadOnlyDictionary<string, string>? _cachedStrings;

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

        // Rebuild only when the culture changes; the neutral check and the merged table are then
        // computed once and reused for every subsequent lookup in the same culture. No lock is taken -
        // a concurrent culture change can at worst make a lookup observe a stale table, never crash
        // (the table reference is snapshotted below) - and the single-culture path is always exact.
        if (!string.Equals(_cachedCultureName, culture.Name, StringComparison.Ordinal))
        {
            // The neutral culture is matched ordinally; string.Equals is null-safe, so a missing
            // NeutralResourcesLanguage attribute (null _neutralCultureName) simply never matches.
            bool isNeutral = string.Equals(culture.Name, _neutralCultureName, StringComparison.Ordinal);
            _cachedCultureIsNeutral = isNeutral;
            _cachedStrings = isNeutral ? null : BuildStrings(culture);
            _cachedCultureName = culture.Name;
        }

        // Snapshot the table reference so a concurrent rebuild cannot turn it null between the check
        // and the lookup.
        if (!_cachedCultureIsNeutral && _cachedStrings is { } strings && strings.TryGetValue(name, out string? value))
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
        IDictionaryEnumerator enumerator = reader.GetEnumerator();
        while (enumerator.MoveNext())
        {
            // Only string values are kept. A file ResourceReader can open is in the default binary
            // format, whose non-string entries are intrinsic types that read without reflection, so
            // reading the value and rejecting non-strings is safe. Files that would need reflection
            // to read are rejected by the ResourceReader constructor (handled in LoadTable).
            if (enumerator.Key is string key && enumerator.Value is string value)
            {
                table[key] = value;
            }
        }

        return table;
    }
}
