// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.ComponentModel;

namespace Touki.Collections;

/// <summary>
///  Base class for lists that have a contiguous backing store in memory.
/// </summary>
public abstract class ContiguousList<T> : ListBase<T> where T : notnull
{
    /// <summary>
    ///  View of the values in the list as a span.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Never modify the list after taking the span. This can lead to undefined behavior, including modifying memory
    ///   in use by other parts of the program (as the list may reallocate its backing storage.)
    ///  </para>
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Browsable(browsable: false)]
    public abstract Span<T> UnsafeValues { get; }

    /// <summary>
    ///  View of the values in the list as a span.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Never modify the list after taking the span. This can lead to undefined behavior, including reading memory
    ///   in use by other parts of the program (as the list may reallocate its backing storage.)
    ///  </para>
    /// </remarks>
    public abstract ReadOnlySpan<T> Values { get; }
}
