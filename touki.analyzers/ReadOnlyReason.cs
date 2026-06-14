// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

/// <summary>
///  Why a receiver location is read-only, and therefore why accessing a non-readonly member through it forces a
///  defensive copy.
/// </summary>
internal enum ReadOnlyReason
{
    /// <summary>An <see langword="in"/> parameter.</summary>
    InParameter,

    /// <summary>A <see langword="ref"/> <see langword="readonly"/> parameter.</summary>
    RefReadOnlyParameter,

    /// <summary>An instance <see langword="readonly"/> field accessed outside its declaring constructor.</summary>
    ReadOnlyField,

    /// <summary>A <see langword="static"/> <see langword="readonly"/> field.</summary>
    StaticReadOnlyField,

    /// <summary>A <see langword="ref"/> <see langword="readonly"/> local.</summary>
    RefReadOnlyLocal,

    /// <summary>A member that returns by <see langword="ref"/> <see langword="readonly"/>.</summary>
    RefReadOnlyReturn,
}
