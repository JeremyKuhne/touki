// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Original license information:
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Touki;

public static partial class SpanExtensions
{
    /// <summary>
    ///  Indicates in which mode <see cref="SpanSplitEnumerator{T}"/> is operating, with regards to how it should interpret its state.
    /// </summary>
    private enum SpanSplitEnumeratorMode
    {
        /// <summary>
        ///  Either a default <see cref="SpanSplitEnumerator{T}"/> was used, or the enumerator has finished enumerating
        ///  and there's no more work to do.
        /// </summary>
        None = 0,

        /// <summary>A single T separator was provided.</summary>
        SingleElement,

        /// <summary>A span of separators was provided, each of which should be treated independently.</summary>
        Any,

        /// <summary>The separator is a span of elements to be treated as a single sequence.</summary>
        Sequence,

        /// <summary>The separator is an empty sequence, such that no splits should be performed.</summary>
        EmptySequence
    }
}
