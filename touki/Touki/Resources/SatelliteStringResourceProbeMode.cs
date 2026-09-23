// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

/// <summary>
///  Controls how direct satellite assembly probe failures affect culture fallback.
/// </summary>
public enum SatelliteStringResourceProbeMode
{
    /// <summary>
    ///  Continue fallback only when the candidate file is absent. Other failures throw.
    /// </summary>
    Strict,

    /// <summary>
    ///  Treat absent, unreadable, malformed, unsupported, incorrectly bundled, or
    ///  identity-mismatched localized candidates as missing and continue fallback. Identity
    ///  mismatches are detected only when
    ///  <see cref="StringResourceManagerOptions.ValidateAssemblyIdentity"/> is enabled.
    /// </summary>
    FallbackOnFailure
}
