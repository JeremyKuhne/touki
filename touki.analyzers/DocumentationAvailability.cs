// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

/// <summary>
///  Describes whether inherited XML documentation was found or could be inspected.
/// </summary>
internal enum DocumentationAvailability
{
    Undocumented,
    Documented,
    Unknown
}