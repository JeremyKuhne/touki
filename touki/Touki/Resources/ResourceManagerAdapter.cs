// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using System.Reflection;
using System.Resources;

namespace Touki.Resources;

/// <summary>
///  Adapts <see cref="ResourceManager"/> to <see cref="StringResourceManager"/> for resources
///  embedded by the deployment toolchain.
/// </summary>
/// <remarks>
///  This manager uses the platform resource implementation for culture fallback. In particular,
///  NativeAOT can resolve localized resources embedded by ILC without calling
///  <see cref="Assembly.GetSatelliteAssembly(CultureInfo)"/>.
/// </remarks>
public sealed class ResourceManagerAdapter : StringResourceManager
{
    private readonly ResourceManager _resourceManager;

    /// <summary>
    ///  Initializes an adapter for embedded resources.
    /// </summary>
    /// <param name="baseName">The root name of the resource table.</param>
    /// <param name="ownerAssembly">The generated accessor's owner assembly.</param>
    /// <exception cref="ArgumentNullException">
    ///  <paramref name="baseName"/> or <paramref name="ownerAssembly"/> is <see langword="null"/>.
    /// </exception>
    public ResourceManagerAdapter(string baseName, Assembly ownerAssembly)
        : base(baseName, ownerAssembly)
    {
        _resourceManager = new(baseName, ownerAssembly);
    }

    /// <inheritdoc/>
    public override string? GetString(string name, CultureInfo? culture)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _resourceManager.GetString(name, culture);
    }

    /// <inheritdoc/>
    public override void ReleaseAllResources() => _resourceManager.ReleaseAllResources();
}
