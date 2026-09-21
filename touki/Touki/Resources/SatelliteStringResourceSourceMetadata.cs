// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

/// <summary>
///  Lazily computed assembly metadata used for satellite probing.
/// </summary>
internal sealed class SatelliteStringResourceSourceMetadata
{
    /// <summary>
    ///  Initializes the source metadata.
    /// </summary>
    /// <param name="satelliteAssemblyFileName">The standard satellite assembly filename.</param>
    /// <param name="neutralCultureName">The declared neutral culture name.</param>
    /// <param name="satelliteContractVersion">The satellite contract version, if declared.</param>
    internal SatelliteStringResourceSourceMetadata(
        string? satelliteAssemblyFileName,
        string? neutralCultureName,
        Version? satelliteContractVersion)
    {
        SatelliteAssemblyFileName = satelliteAssemblyFileName;
        NeutralCultureName = neutralCultureName;
        SatelliteContractVersion = satelliteContractVersion;
    }

    /// <summary>
    ///  The standard satellite assembly filename, if the source assembly has a name.
    /// </summary>
    internal string? SatelliteAssemblyFileName { get; }

    /// <summary>
    ///  The declared neutral culture name, if any.
    /// </summary>
    internal string? NeutralCultureName { get; }

    /// <summary>
    ///  The satellite contract version, if declared.
    /// </summary>
    internal Version? SatelliteContractVersion { get; }
}