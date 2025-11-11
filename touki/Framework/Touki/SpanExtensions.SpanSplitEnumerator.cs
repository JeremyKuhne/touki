// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Original license information:
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;

namespace Touki;

public static partial class SpanExtensions
{
    private const string WhiteSpaceChars =
        "\t\n\v\f\r\u0020\u0085\u00a0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200a\u2028\u2029\u202f\u205f\u3000";

    /// <summary>
    /// Returns a type that allows for enumeration of each element within a split span
    /// using the provided separator character.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="source">The source span to be enumerated.</param>
    /// <param name="separator">The separator character to be used to split the provided span.</param>
    /// <returns>Returns a <see cref="SpanSplitEnumerator{T}"/>.</returns>
    public static SpanSplitEnumerator<T> Split<T>(this ReadOnlySpan<T> source, T separator) where T : IEquatable<T> =>
        new SpanSplitEnumerator<T>(source, separator);

    /// <summary>
    /// Returns a type that allows for enumeration of each element within a split span
    /// using the provided separator span.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="source">The source span to be enumerated.</param>
    /// <param name="separator">The separator span to be used to split the provided span.</param>
    /// <returns>Returns a <see cref="SpanSplitEnumerator{T}"/>.</returns>
    public static SpanSplitEnumerator<T> Split<T>(this ReadOnlySpan<T> source, ReadOnlySpan<T> separator) where T : IEquatable<T> =>
        new SpanSplitEnumerator<T>(source, separator, treatAsSingleSeparator: true);

    /// <summary>
    /// Returns a type that allows for enumeration of each element within a split span
    /// using any of the provided elements.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="source">The source span to be enumerated.</param>
    /// <param name="separators">The separators to be used to split the provided span.</param>
    /// <returns>Returns a <see cref="SpanSplitEnumerator{T}"/>.</returns>
    /// <remarks>
    /// If <typeparamref name="T"/> is <see cref="char"/> and if <paramref name="separators"/> is empty,
    /// all Unicode whitespace characters are used as the separators. This matches the behavior of when
    /// <see cref="string.Split(char[])"/> and related overloads are used with an empty separator array.
    /// </remarks>
    public static SpanSplitEnumerator<T> SplitAny<T>(
        this ReadOnlySpan<T> source,
        [UnscopedRef] params ReadOnlySpan<T> separators) where T : IEquatable<T> => new SpanSplitEnumerator<T>(source, separators);

    /// <summary>
    ///  Enables enumerating each split within a <see cref="ReadOnlySpan{T}"/> that has been divided using one or more separators.
    /// </summary>
    /// <typeparam name="T">The type of items in the <see cref="SpanSplitEnumerator{T}"/>.</typeparam>
    public ref struct SpanSplitEnumerator<T> : IEnumerator<Range> where T : IEquatable<T>
    {
        /// <summary>The input span being split.</summary>
        private readonly ReadOnlySpan<T> _source;

        /// <summary>A single separator to use when <see cref="_splitMode"/> is <see cref="SpanSplitEnumeratorMode.SingleElement"/>.</summary>
        private readonly T _separator = default!;

        /// <summary>
        ///  A separator span to use when <see cref="_splitMode"/> is <see cref="SpanSplitEnumeratorMode.Sequence"/>
        ///  (in which case it's treated as a single separator) or <see cref="SpanSplitEnumeratorMode.Any"/>
        ///  (in which case it's treated as a set of separators).
        /// </summary>
        private readonly ReadOnlySpan<T> _separatorBuffer;

        /// <summary>
        ///  Mode that dictates how the instance was configured and how its fields should be used in <see cref="MoveNext"/>.
        /// </summary>
        private SpanSplitEnumeratorMode _splitMode;

        /// <summary>The inclusive starting index in <see cref="_source"/> of the current range.</summary>
        private int _startCurrent = 0;

        /// <summary>The exclusive ending index in <see cref="_source"/> of the current range.</summary>
        private int _endCurrent = 0;

        /// <summary>The index in <see cref="_source"/> from which the next separator search should start.</summary>
        private int _startNext = 0;

        private GCHandle _whiteSpacePin;

        /// <summary>Gets an enumerator that allows for iteration over the split span.</summary>
        /// <returns>Returns a <see cref="SpanSplitEnumerator{T}"/> that can be used to iterate over the split span.</returns>
        public readonly SpanSplitEnumerator<T> GetEnumerator() => this;

        /// <summary>Gets the source span being enumerated.</summary>
        /// <returns>Returns the <see cref="ReadOnlySpan{T}"/> that was provided when creating this enumerator.</returns>
        public readonly ReadOnlySpan<T> Source => _source;

        /// <summary>Gets the current element of the enumeration.</summary>
        /// <returns>Returns a <see cref="Range"/> instance that indicates the bounds of the current element within the source span.</returns>
        public readonly Range Current => new Range(_startCurrent, _endCurrent);

        /// <summary>Initializes the enumerator for <see cref="SpanSplitEnumeratorMode.Any"/>.</summary>
        internal unsafe SpanSplitEnumerator(ReadOnlySpan<T> source, ReadOnlySpan<T> separators)
        {
            _source = source;

            if (typeof(T) == typeof(char) && separators.Length == 0)
            {
                // Match string.Split behavior when given an empty char[] as the separator: use all Unicode whitespace
                // characters as the separators. (There currently isn't a way to reinterpret the WhiteSpaceChars,
                // even though we know T is char, so we pin and use a pointer.)

                _whiteSpacePin = GCHandle.Alloc(WhiteSpaceChars, GCHandleType.Pinned);
                fixed (char* whiteSpacePtr = WhiteSpaceChars)
                {
                    _separatorBuffer = new(
                        whiteSpacePtr,
                        WhiteSpaceChars.Length);
                }
            }
            else
            {
                _separatorBuffer = separators;
            }

            _splitMode = SpanSplitEnumeratorMode.Any;
        }

        /// <summary>
        ///  Initializes the enumerator for <see cref="SpanSplitEnumeratorMode.Sequence"/>
        ///  (or <see cref="SpanSplitEnumeratorMode.EmptySequence"/> if the separator is empty).
        /// </summary>
        /// <remarks><paramref name="treatAsSingleSeparator"/> must be true.</remarks>
        internal SpanSplitEnumerator(ReadOnlySpan<T> source, ReadOnlySpan<T> separator, bool treatAsSingleSeparator)
        {
            Debug.Assert(treatAsSingleSeparator, "Should only ever be called as true; exists to differentiate from separators overload");

            _source = source;
            _separatorBuffer = separator;
            _splitMode = separator.Length == 0 ?
                SpanSplitEnumeratorMode.EmptySequence :
                SpanSplitEnumeratorMode.Sequence;
        }

        /// <summary>Initializes the enumerator for <see cref="SpanSplitEnumeratorMode.SingleElement"/>.</summary>
        internal SpanSplitEnumerator(ReadOnlySpan<T> source, T separator)
        {
            _source = source;
            _separator = separator;
            _splitMode = SpanSplitEnumeratorMode.SingleElement;
        }

        /// <summary>
        ///  Advances the enumerator to the next element of the enumeration.
        /// </summary>
        /// <returns>
        ///  <see langword="true"/> if the enumerator was successfully advanced to the next element; <see langword="false"/>
        ///  if the enumerator has passed the end of the enumeration.
        /// </returns>
        public bool MoveNext()
        {
            // Search for the next separator index.
            int separatorIndex, separatorLength;
            switch (_splitMode)
            {
                case SpanSplitEnumeratorMode.None:
                    return false;

                case SpanSplitEnumeratorMode.SingleElement:
                    separatorIndex = _source[_startNext..].IndexOf(_separator);
                    separatorLength = 1;
                    break;

                case SpanSplitEnumeratorMode.Any:
                    separatorIndex = _source[_startNext..].IndexOfAny(_separatorBuffer);
                    separatorLength = 1;
                    break;

                case SpanSplitEnumeratorMode.Sequence:
                    separatorIndex = _source[_startNext..].IndexOf(_separatorBuffer);
                    separatorLength = _separatorBuffer.Length;
                    break;

                default:
                    Debug.Assert(_splitMode == SpanSplitEnumeratorMode.EmptySequence, $"Unknown split mode: {_splitMode}");
                    separatorIndex = -1;
                    separatorLength = 1;
                    break;
            }

            _startCurrent = _startNext;
            if (separatorIndex >= 0)
            {
                _endCurrent = _startCurrent + separatorIndex;
                _startNext = _endCurrent + separatorLength;
            }
            else
            {
                _startNext = _endCurrent = _source.Length;

                // Set _splitMode to None so that subsequent MoveNext calls will return false.
                _splitMode = SpanSplitEnumeratorMode.None;
            }

            return true;
        }

        /// <inheritdoc />
        readonly object IEnumerator.Current => Current;

        /// <inheritdoc />
        void IEnumerator.Reset() => throw new NotSupportedException();

        /// <inheritdoc />
        readonly void IDisposable.Dispose()
        {
            if (_whiteSpacePin.IsAllocated)
            {
                _whiteSpacePin.Free();
            }
        }
    }
}
