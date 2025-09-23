// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Threading;

namespace Touki;

/// <summary>
///  Light weight multithreaded fixed size cache class.
/// </summary>
public class Cache<T> : DisposableBase where T : class, new()
{
    [ThreadStatic]
    private static T? t_localItem;

    private readonly T?[] _itemsCache;

    /// <summary>
    ///  Create a cache with space for the specified number of items.
    /// </summary>
    public Cache(int cacheSpace)
    {
        if (cacheSpace < 1)
        {
            cacheSpace = Environment.ProcessorCount * 4;
        }

        _itemsCache = new T[cacheSpace];
    }

    /// <summary>
    ///  Get an item from the cache or create one if none are available.
    /// </summary>
    public virtual T Acquire()
    {
        T? item = t_localItem;
        if (item is not null)
        {
            t_localItem = null;
        }
        else
        {
            for (int i = 0; i < _itemsCache.Length; i++)
            {
                item = Interlocked.Exchange(ref _itemsCache[i], null);
                if (item is not null)
                {
                    break;
                }
            }
        }

        item ??= new();
        return item;
    }

    /// <summary>
    ///  Release an item back to the cache, disposing if no room is available.
    /// </summary>
    public virtual void Release(T item)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        if (t_localItem is null)
        {
            t_localItem = item;
            return;
        }

        T? temp = item;

        for (int i = 0; i < _itemsCache.Length; i++)
        {
            temp = Interlocked.Exchange(ref _itemsCache[i], temp);
            if (temp is null)
            {
                return;
            }
        }

        // We weren't able to cache the item, so dispose it.
        (temp as IDisposable)?.Dispose();
    }

    /// <inheritdoc cref="DisposableBase.Dispose(bool)"/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        (t_localItem as IDisposable)?.Dispose();
        t_localItem = null;

        for (int i = 0; i < _itemsCache.Length; i++)
        {
            (_itemsCache[i] as IDisposable)?.Dispose();
            _itemsCache[i] = null;
        }
    }
}
