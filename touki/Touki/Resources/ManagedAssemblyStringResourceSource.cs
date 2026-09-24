// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Resources;
using System.Threading;

namespace Touki.Resources;

/// <summary>
///  Opens one managed resource assembly as a memory-mapped data source.
/// </summary>
internal sealed class ManagedAssemblyStringResourceSource
{
    private readonly Func<string, MappedMemoryManager> _openFile;
    private readonly ManagedAssemblyIdentity? _expectedOwnerIdentity;
    private ResourceAssemblyMetadata? _metadata;

    /// <summary>
    ///  Initializes a managed assembly file source.
    /// </summary>
    /// <param name="assemblyFile">The managed assembly file.</param>
    /// <param name="openFile">The function that memory-maps the file.</param>
    /// <param name="expectedOwnerIdentity">The expected owner identity, when validation is enabled.</param>
    internal ManagedAssemblyStringResourceSource(
        string assemblyFile,
        Func<string, MappedMemoryManager> openFile,
        ManagedAssemblyIdentity? expectedOwnerIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(assemblyFile);
        ArgumentNullException.ThrowIfNull(openFile);
        AssemblyFile = Path.GetFullPath(assemblyFile);
        _openFile = openFile;
        _expectedOwnerIdentity = expectedOwnerIdentity;
    }

    /// <summary>
    ///  The fully qualified managed assembly file.
    /// </summary>
    internal string AssemblyFile { get; }

    /// <summary>
    ///  The metadata read during the most recent successful assembly parse.
    /// </summary>
    internal ResourceAssemblyMetadata Metadata =>
        Volatile.Read(ref _metadata)
            ?? throw new InvalidOperationException("The managed assembly metadata has not been loaded.");

    /// <summary>
    ///  Loads the named resource table and transfers the mapped file to it.
    /// </summary>
    /// <param name="resourceName">The exact manifest resource name.</param>
    /// <param name="options">The string resource options.</param>
    /// <returns>The indexed string table.</returns>
    internal IndexedStringResourceTable LoadTable(
        string resourceName,
        StringResourceManagerOptions options)
    {
        MappedMemoryManager assemblyFile = _openFile(AssemblyFile);
        (
            IndexedStringResourceTable? table,
            ResourceAssemblyMetadata metadata
        ) = StringResourceTableLoader.LoadIndexedTableAndMetadataFromAssembly(
            assemblyFile.Memory,
            resourceName,
            options,
            assemblyFile,
            _expectedOwnerIdentity);

        Volatile.Write(ref _metadata, metadata);
        return table
            ?? throw new MissingManifestResourceException(
                $"The assembly '{AssemblyFile}' does not contain '{resourceName}'.");
    }
}
