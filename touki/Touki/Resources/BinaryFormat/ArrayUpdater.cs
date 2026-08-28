// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from dotnet/runtime at 7aa830a03599a8255c2c4abf2947afc5b346cc6f (MIT licensed):
// src/libraries/System.Resources.Extensions/src/System/Resources/Extensions/BinaryFormat/

using System.Formats.Nrbf;

namespace Touki.Resources.BinaryFormat;

/// <summary>
///  Replaces an array element at a captured multidimensional index after its referenced value is available.
/// </summary>
internal sealed class ArrayUpdater : ValueUpdater
{
    private readonly int[] _indices;

    /// <summary>
    ///  Initializes an updater for an array element.
    /// </summary>
    /// <param name="objectId">The identifier of the array that owns the element.</param>
    /// <param name="valueId">The identifier of the referenced value.</param>
    /// <param name="indices">The multidimensional indices of the element to update.</param>
    internal ArrayUpdater(SerializationRecordId objectId, SerializationRecordId valueId, int[] indices)
        : base(objectId, valueId)
    {
        _indices = indices;
    }

    /// <inheritdoc/>
    internal override void UpdateValue(IDictionary<SerializationRecordId, object> objects)
    {
        object value = objects[ValueId];
        Array array = (Array)objects[ObjectId];
        array.SetValue(value, _indices);
    }
}