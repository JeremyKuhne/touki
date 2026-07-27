// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Touki.Analyzers.NamingStyles;

/// <summary>
///  How the words of a name are capitalized.
/// </summary>
internal enum Capitalization
{
    /// <summary>
    ///  Every word begins with an upper case character. <c>PascalCase</c>.
    /// </summary>
    PascalCase,

    /// <summary>
    ///  The first word begins with a lower case character, the rest with upper case. <c>camelCase</c>.
    /// </summary>
    CamelCase,

    /// <summary>
    ///  The first word begins with an upper case character, the rest with lower case. <c>First upper</c>.
    /// </summary>
    FirstUpper,

    /// <summary>
    ///  Every character is upper case. <c>ALL_UPPER</c>.
    /// </summary>
    AllUpper,

    /// <summary>
    ///  Every character is lower case. <c>all_lower</c>.
    /// </summary>
    AllLower
}
