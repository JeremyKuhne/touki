// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

/// <summary>
///  Controls string resource table loading behavior.
/// </summary>
[Flags]
public enum StringResourceManagerOptions
{
    /// <summary>
    ///  Reject tables containing null or non-string resources.
    /// </summary>
    None = 0,

    /// <summary>
    ///  Permit non-string resources but do not retain their names or values. A string lookup for an
    ///  ignored resource behaves as though the name is missing. Null resources remain invalid.
    /// </summary>
    IgnoreNonStringResources = 1,

    /// <summary>
    ///  Validate parsed satellite identities against their owner. When an expected owner assembly is
    ///  supplied, validate its simple name, culture, version, and public key token on each file load.
    /// </summary>
    ValidateAssemblyIdentity = 2
}