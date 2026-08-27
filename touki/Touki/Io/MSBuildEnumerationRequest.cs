// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Describes one MSBuild-compatible file enumeration request.
/// </summary>
public readonly struct MSBuildEnumerationRequest
{
    /// <summary>
    ///  Constructs an enumeration request.
    /// </summary>
    /// <param name="include">The include specification.</param>
    /// <param name="projectDirectory">The project directory used to resolve relative specifications.</param>
    /// <param name="excludes">The optional semicolon-separated exclude specifications.</param>
    /// <param name="enumerationOptions">The optional file-system enumeration options.</param>
    /// <param name="allowDriveEnumeration">Whether recursive drive-root enumeration is allowed.</param>
    public MSBuildEnumerationRequest(
        string include,
        string? projectDirectory = null,
        string? excludes = null,
        EnumerationOptions? enumerationOptions = null,
        bool allowDriveEnumeration = false)
    {
        ArgumentNullException.ThrowIfNull(include);
        Include = include;
        ProjectDirectory = projectDirectory;
        Excludes = excludes;
        EnumerationOptions = enumerationOptions;
        AllowDriveEnumeration = allowDriveEnumeration;
    }

    /// <summary>
    ///  Gets the include specification.
    /// </summary>
    public string Include { get; }

    /// <summary>
    ///  Gets the optional semicolon-separated exclude specifications.
    /// </summary>
    public string? Excludes { get; }

    /// <summary>
    ///  Gets the project directory used to resolve relative specifications.
    /// </summary>
    public string? ProjectDirectory { get; }

    /// <summary>
    ///  Gets optional file-system enumeration options. <see langword="null"/> selects the defaults.
    /// </summary>
    public EnumerationOptions? EnumerationOptions { get; }

    /// <summary>
    ///  Gets whether recursive drive-root enumeration is allowed.
    /// </summary>
    public bool AllowDriveEnumeration { get; }
}
