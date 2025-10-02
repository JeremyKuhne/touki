// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Original license follows:
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Touki.Collections;

public sealed partial class SinglyLinkedList<T>
{
    /// <summary>
    ///  Node in the list.
    /// </summary>
    public class Node(T value)
    {
        /// <summary>
        ///  Next node in the list or <see langword="null"/> if this is the last node.
        /// </summary>
        public Node? Next { get; set; }

        /// <summary>
        ///  Value of the node.
        /// </summary>
        public T Value { get; set; } = value;

        /// <summary>
        ///  Implicitly converts the node to its value.
        /// </summary>
        public static implicit operator T(Node? node) => node is null ? default! : node.Value;
    }
}

