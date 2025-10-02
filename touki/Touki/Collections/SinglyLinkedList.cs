// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Original license follows:
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Touki.Collections;

/// <summary>
///  Simple singly linked list implementation.
/// </summary>
/// <devdoc>
///  This class is used in <see cref="RefCountedCache{TObject, TCacheEntryData, TKey}"/> which is performance
///  sensitive. Do not make changes without validating performance impacts.
/// </devdoc>
public sealed partial class SinglyLinkedList<T>
{
    /// <summary>
    ///  Constructs a new instance of the <see cref="SinglyLinkedList{T}"/> class.
    /// </summary>
    public SinglyLinkedList() { }

    /// <summary>
    ///  Count of items in the list.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    ///  First node in the list or <see langword="null"/> if the list is empty.
    /// </summary>
    public Node? First { get; private set; }

    /// <summary>
    ///  Last node in the list or <see langword="null"/> if the list is empty.
    /// </summary>
    public Node? Last { get; private set; }

    /// <summary>
    ///  Adds a new node with the given <paramref name="value"/> at the front of the list.
    /// </summary>
    public Node AddFirst(T value)
    {
        Node node = new(value);

        if (Count == 0)
        {
            // Nothing in the list yet
            First = Last = node;
        }
        else
        {
            // Last doesn't change, insert in the front
            node.Next = First;
            First = node;
        }

        Count++;
        return node;
    }

    /// <summary>
    ///  Adds a new node with the given <paramref name="value"/> at the end of the list.
    /// </summary>
    public Node AddLast(T value)
    {
        Node node = new(value);

        if (Count == 0)
        {
            // Nothing in the list yet
            First = Last = node;
        }
        else
        {
            // Add at the end
            Debug.Assert(First is not null && Last is not null);
            Last!.Next = node;
            Last = node;
        }

        Count++;
        return node;
    }

    /// <summary>
    ///  Returns an enumerator for the list.
    /// </summary>
    public Enumerator GetEnumerator() => new(this);
}

