// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

/// <summary>
///  The selected source for localized string tables.
/// </summary>
internal enum SatelliteStringResourceSourceKind
{
    /// <summary>
    ///  Bind satellite assemblies through the runtime.
    /// </summary>
    RuntimeSatellites,

    /// <summary>
    ///  Read loose binary resource files.
    /// </summary>
    ResourcesDirectory,

    /// <summary>
    ///  Parse satellite assemblies directly as data.
    /// </summary>
    SatelliteDirectory
}