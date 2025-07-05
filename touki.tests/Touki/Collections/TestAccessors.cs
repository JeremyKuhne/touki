// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information


namespace Touki.Collections;

public static class TestAccessors
{
    public class SingleOptimizedArrayListAccessor<T> : TestAccessor<SingleOptimizedList<T>> where T : notnull
    {
        public SingleOptimizedArrayListAccessor(SingleOptimizedList<T> instance) : base(instance) { }

        public bool HasItem => Dynamic._hasItem;
        public T Item => Dynamic._item;
        public ArrayPoolList<T>? BackingList => Dynamic._backingList;
    }

    public static SingleOptimizedArrayListAccessor<T> TestAccessor<T>(this SingleOptimizedList<T> list)
        where T : notnull => new SingleOptimizedArrayListAccessor<T>(list);
}
