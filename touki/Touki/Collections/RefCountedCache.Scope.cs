// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Original license follows:
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Touki.Collections;

public abstract partial class RefCountedCache<TValue, TCacheEntryData, TKey>
{
    /// <summary>
    ///  Disposable struct that manages reference counting of <see cref="CacheEntry"/>.
    /// </summary>
    // Ref-counts a CacheEntry: the entry constructor calls AddRef and Dispose calls Release. Copying the scope by
    // value would duplicate the single logical reference without an extra AddRef, so disposing both copies would
    // Release twice and corrupt the count - hence [NonCopyable]. [MustDispose] (the TOUKI0010 analyzer) reports a
    // scope that is never disposed at compile time.
    [NonCopyable]
    [MustDispose]
    public readonly ref struct Scope
    {
        private readonly TValue _object;
        private readonly CacheEntry _entry;

        /// <summary>
        ///  Constructor to hold an uncached object. Used to wrap something not coming from the cache in a scope
        ///  so it can be abstracted for the end users of a given API.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   Currently we don't dispose the <paramref name="object"/> as we don't need to in our usages. If this
        ///   becomes necessary we can add a bool to track whether or not we should dispose it.
        ///  </para>
        /// </remarks>
        public Scope(TValue @object)
        {
            _entry = default!;
            _object = @object;
        }

        /// <summary>
        ///  The constructor to use when you have a <see cref="CacheEntry"/> from the cache.
        /// </summary>
        public Scope(CacheEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            _object = default!;
            _entry = entry;
            _entry.AddRef();
        }

        /// <summary>
        ///  Returns the cache entry data if this scope is associated with a cached entry.
        /// </summary>
        public bool TryGetCacheData(out TCacheEntryData? data)
        {
            if (_entry is null)
            {
                data = default;
                return false;
            }

            data = _entry.Data;
            return true;
        }

        /// <summary>
        ///  The scoped object.
        /// </summary>
        public TValue Object => this;

        /// <summary>
        ///  Reference count of the underlying <see cref="CacheEntry"/> or -1 if this scope is not
        ///  reference counted.
        /// </summary>
        public int RefCount => _entry?.RefCount ?? -1;

        /// <summary>
        ///  Implicit conversion to the "target" type, i.e. <typeparamref name="TValue"/>.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   This is somewhat dangerous as implicit casting in the using statement will leak the scope. Not doing
        ///   this, however, makes usage with APIs difficult. The <c>[MustDispose]</c> analyzer (TOUKI0010) treats
        ///   the conversion as a use rather than a dispose, so a scope that is only implicitly converted is flagged.
        ///  </para>
        /// </remarks>
        public static implicit operator TValue(in Scope scope)
        {
            CacheEntry entry = scope._entry;
            return entry is null ? scope._object : entry.Object;
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose() => _entry?.RemoveRef();
    }
}

