// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;

namespace Touki.Resources;

/// <summary>
///  Resolves strings from one binary <c>.resources</c> file, one embedded resource in an
///  already-loaded assembly, or a stream supplied on demand.
/// </summary>
/// <remarks>
///  <para>
///   Resource loading is deferred until the first lookup. The manager retains the indexed source
///   backing, decodes only requested strings, and caches the last decoded string for subsequent
///   lookup without allocation.
///  </para>
///  <para>
///   The <c>culture</c> argument to <see cref="GetString(string, CultureInfo?)"/> is
///   ignored because this manager represents exactly one resource table.
///  </para>
///  <para>
///   Resource data is expected to be a trusted output of the application's build and deployment
///   pipeline. A null resource always rejects the table. By default any non-string resource rejects
///   the table; <see cref="StringResourceManagerOptions.IgnoreNonStringResources"/> skips non-string
///   entries without retaining their names or values.
///  </para>
///  <para>
///   Files are memory-mapped and runtime manifest resources are read directly from their unmanaged
///   backing. Other stream-factory sources must be readable and seekable. A source backing is opened
///   once per cache generation, retained until <see cref="ReleaseAllResources()"/>, and never copied
///   in full merely to parse it.
///  </para>
/// </remarks>
public class StringResourceManager
{
    private string? _baseName;
    private readonly object _source;
    private readonly bool _ownsNeutralResources;
    private readonly StringResourceManagerOptions _options;
    private object? _loadGate;
    private int _generation;
    private volatile StringResourceTableCache? _cache;

    /// <summary>
    ///  Initializes a manager for the binary <c>.resources</c> file at <paramref name="resourcesFile"/>.
    /// </summary>
    /// <param name="resourcesFile">The path of a default-format version 2 <c>.resources</c> file.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resourcesFile"/> is <see langword="null"/>.</exception>
    public StringResourceManager(string resourcesFile)
        : this(resourcesFile, StringResourceManagerOptions.None)
    {
    }

    /// <summary>
    ///  Initializes a manager for the binary <c>.resources</c> file at <paramref name="resourcesFile"/>.
    /// </summary>
    /// <param name="resourcesFile">The path of a default-format version 2 <c>.resources</c> file.</param>
    /// <param name="options">The resource loading options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resourcesFile"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> contains an unknown flag.</exception>
    public StringResourceManager(
        string resourcesFile,
        StringResourceManagerOptions options)
    {
        ArgumentNullException.ThrowIfNull(resourcesFile);
        options.Validate();
        _source = resourcesFile;
        _options = options;
    }

    /// <summary>
    ///  Initializes a manager for the named resource table in an already-loaded assembly.
    /// </summary>
    /// <param name="baseName">The root name of the resource, without the <c>.resources</c> extension.</param>
    /// <param name="assembly">The loaded assembly containing the resource.</param>
    /// <exception cref="ArgumentNullException">
    ///  <paramref name="baseName"/> or <paramref name="assembly"/> is <see langword="null"/>.
    /// </exception>
    public StringResourceManager(string baseName, Assembly assembly)
        : this(baseName, assembly, StringResourceManagerOptions.None)
    {
    }

    /// <summary>
    ///  Initializes a manager for the named resource table in an already-loaded assembly.
    /// </summary>
    /// <param name="baseName">The root name of the resource, without the <c>.resources</c> extension.</param>
    /// <param name="assembly">The loaded assembly containing the resource.</param>
    /// <param name="options">The resource loading options.</param>
    /// <exception cref="ArgumentNullException">
    ///  <paramref name="baseName"/> or <paramref name="assembly"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> contains an unknown flag.</exception>
    public StringResourceManager(
        string baseName,
        Assembly assembly,
        StringResourceManagerOptions options)
    {
        ArgumentNullException.ThrowIfNull(baseName);
        ArgumentNullException.ThrowIfNull(assembly);
        options.Validate();
        _baseName = baseName;
        _source = assembly;
        _options = options;
    }

    /// <summary>
    ///  Initializes a manager for a binary <c>.resources</c> stream supplied on demand.
    /// </summary>
    /// <param name="baseName">The base name of the resource table.</param>
    /// <param name="streamFactory">
    ///  A factory that returns the resource stream positioned at the first byte to parse.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///  <paramref name="baseName"/> or <paramref name="streamFactory"/> is <see langword="null"/>.
    /// </exception>
    public StringResourceManager(string baseName, Func<Stream> streamFactory)
        : this(baseName, streamFactory, StringResourceManagerOptions.None)
    {
    }

    /// <summary>
    ///  Initializes a manager for a binary <c>.resources</c> stream supplied on demand.
    /// </summary>
    /// <param name="baseName">The base name of the resource table.</param>
    /// <param name="streamFactory">
    ///  A factory that returns the resource stream positioned at the first byte to parse.
    /// </param>
    /// <param name="options">The resource loading options.</param>
    /// <exception cref="ArgumentNullException">
    ///  <paramref name="baseName"/> or <paramref name="streamFactory"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> contains an unknown flag.</exception>
    public StringResourceManager(
        string baseName,
        Func<Stream> streamFactory,
        StringResourceManagerOptions options)
    {
        ArgumentNullException.ThrowIfNull(baseName);
        ArgumentNullException.ThrowIfNull(streamFactory);
        options.Validate();
        _baseName = baseName;
        _source = streamFactory;
        _options = options;
    }

    /// <summary>
    ///  Initializes a manager that delegates neutral lookups to <paramref name="neutralResources"/>.
    /// </summary>
    /// <param name="baseName">The base name of the resource table.</param>
    /// <param name="neutralResources">The manager that supplies neutral strings.</param>
    /// <param name="ownsNeutralResources">
    ///  Whether <see cref="ReleaseAllResources()"/> releases <paramref name="neutralResources"/>.
    /// </param>
    /// <param name="options">The resource loading options.</param>
    protected StringResourceManager(
        string baseName,
        StringResourceManager neutralResources,
        bool ownsNeutralResources,
        StringResourceManagerOptions options)
    {
        ArgumentNullException.ThrowIfNull(baseName);
        ArgumentNullException.ThrowIfNull(neutralResources);
        options.Validate();
        _baseName = baseName;
        _source = neutralResources;
        _ownsNeutralResources = ownsNeutralResources;
        _options = options;
    }

    /// <summary>
    ///  The base name of the resource table.
    /// </summary>
    public virtual string BaseName
    {
        get
        {
            string? baseName = _baseName;
            if (baseName is not null)
            {
                return baseName;
            }

            baseName = Path.GetFileNameWithoutExtension((string)_source);
            return Interlocked.CompareExchange(ref _baseName, baseName, comparand: null) ?? baseName;
        }
    }

    /// <summary>
    ///  The resource loading options.
    /// </summary>
    public StringResourceManagerOptions Options => _options;

    /// <summary>
    ///  Gets whether resource names are matched without regard to case. Assigning a value is not
    ///  supported.
    /// </summary>
    /// <exception cref="NotSupportedException">The property is assigned a value.</exception>
    public bool IgnoreCase
    {
        get => false;
        set => throw new NotSupportedException("Case-insensitive resource lookup is not supported.");
    }

    /// <summary>
    ///  Gets the string with the given name.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <returns>The string value, or <see langword="null"/> when the resource does not exist.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The configured source is not a valid resource file.</exception>
    /// <exception cref="BadImageFormatException">
    ///  The configured source contains malformed data or a null resource.
    /// </exception>
    /// <exception cref="NotSupportedException">
    ///  The source format is unsupported, or the table contains a non-string resource and
    ///  <see cref="StringResourceManagerOptions.IgnoreNonStringResources"/> is not set.
    /// </exception>
    /// <exception cref="IOException">The configured file or stream cannot be read.</exception>
    public virtual string? GetString(string name) => GetString(name, culture: null);

    /// <summary>
    ///  Gets the string with the given name. The culture is ignored because this manager represents
    ///  exactly one table.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="culture">Ignored.</param>
    /// <returns>The string value, or <see langword="null"/> when the resource does not exist.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The configured source is not a valid resource file.</exception>
    /// <exception cref="BadImageFormatException">
    ///  The configured source contains malformed data or a null resource.
    /// </exception>
    /// <exception cref="NotSupportedException">
    ///  The source format is unsupported, or the table contains a non-string resource and
    ///  <see cref="StringResourceManagerOptions.IgnoreNonStringResources"/> is not set.
    /// </exception>
    /// <exception cref="IOException">The configured file or stream cannot be read.</exception>
    public virtual string? GetString(string name, CultureInfo? culture)
    {
        ArgumentNullException.ThrowIfNull(name);
        StringResourceLookupKind result = LookupString(name, out string? value);
        return result switch
        {
            StringResourceLookupKind.Found => value,
            StringResourceLookupKind.Missing => null,
            _ => throw new InvalidOperationException("The resource lookup result is invalid.")
        };
    }

    /// <summary>
    ///  Releases the cached string table. The next lookup reloads it from the configured source.
    /// </summary>
    public virtual void ReleaseAllResources() => ReleaseAllResourcesCore(waitingForLoad: null);

    /// <summary>
    ///  Releases resources and invokes <paramref name="waitingForLoad"/> after detecting a contended
    ///  load gate.
    /// </summary>
    /// <param name="waitingForLoad">The callback invoked before waiting for the load gate.</param>
    internal void ReleaseAllResources(Action waitingForLoad)
    {
        ArgumentNullException.ThrowIfNull(waitingForLoad);
        ReleaseAllResourcesCore(waitingForLoad);
    }

    private void ReleaseAllResourcesCore(Action? waitingForLoad)
    {
        if (_source is StringResourceManager neutralResources)
        {
            if (_ownsNeutralResources)
            {
                neutralResources.ReleaseAllResources();
            }

            return;
        }

        object loadGate = GetLoadGate();
        bool lockTaken = Monitor.TryEnter(loadGate);
        if (!lockTaken)
        {
            waitingForLoad?.Invoke();
            Monitor.Enter(loadGate, ref lockTaken);
        }

        try
        {
            StringResourceTableCache? cache = _cache;
            _generation = unchecked(_generation + 1);
            _cache = null;
            cache?.Release();
        }
        finally
        {
            if (lockTaken)
            {
                Monitor.Exit(loadGate);
            }
        }
    }

    /// <summary>
    ///  The source assembly, if this manager is assembly-backed.
    /// </summary>
    internal Assembly? SourceAssembly => _source as Assembly;

    /// <summary>
    ///  Looks up a name in this manager's single resource table.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="value">The string value when found.</param>
    /// <returns>The lookup result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal StringResourceLookupKind LookupString(string name, out string? value)
    {
        if (_source is StringResourceManager neutralResources)
        {
            value = neutralResources.GetString(name);
            return value is null ? StringResourceLookupKind.Missing : StringResourceLookupKind.Found;
        }

        while (true)
        {
            int generation = Volatile.Read(ref _generation);
            StringResourceTableCache? cache = _cache;
            if (cache is null || cache.Generation != generation)
            {
                cache = GetOrLoadCache();
            }

            StringResourceLookupKind result = cache.Table.Lookup(name, out value);
            if (result != StringResourceLookupKind.Stale)
            {
                return result;
            }
        }
    }

    private StringResourceTableCache GetOrLoadCache()
    {
        lock (GetLoadGate())
        {
            int generation = Volatile.Read(ref _generation);
            StringResourceTableCache? cache = _cache;
            if (cache is not null && cache.Generation == generation)
            {
                return cache;
            }

            try
            {
                cache = new(generation, LoadTable());
            }
            catch (Exception exception)
            {
                cache = new(generation, exception);
            }

            _cache = cache;
            return cache;
        }
    }

    private object GetLoadGate()
    {
        object? gate = Volatile.Read(ref _loadGate);
        if (gate is not null)
        {
            return gate;
        }

        object newGate = new();
        return Interlocked.CompareExchange(ref _loadGate, newGate, comparand: null) ?? newGate;
    }

    private IndexedStringResourceTable LoadTable()
    {
        if (_source is Assembly assembly)
        {
            string resourceName = $"{BaseName}.resources";

            return StringResourceTableLoader.LoadIndexedTableFromAssembly(
                assembly,
                resourceName,
                _options)
                ?? throw new MissingManifestResourceException(
                    $"The assembly '{assembly.FullName}' does not contain '{resourceName}'.");
        }

        if (_source is string resourcesFile)
        {
            return IndexedStringResourceTable.Create(
                RawResourceReader.CreateFromFile(resourcesFile),
                _options);
        }

        Func<Stream> streamFactory = (Func<Stream>)_source;
        Stream stream = streamFactory()
            ?? throw new InvalidOperationException("The resource stream factory returned null.");

        return StringResourceTableLoader.LoadIndexedTableFromResourcesStream(stream, _options);
    }

}