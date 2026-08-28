// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from dotnet/runtime at 7aa830a03599a8255c2c4abf2947afc5b346cc6f (MIT licensed):
// src/libraries/System.Resources.Extensions/src/System/Resources/Extensions/BinaryFormat/

using System.Formats.Nrbf;

namespace Touki.Resources.BinaryFormat;

/// <summary>
///  Identifies a referenced value that must be written back to its owner after dependency completion.
/// </summary>
internal abstract class ValueUpdater
{
    /// <summary>
    ///  Initializes an updater for a referenced value and its owning object.
    /// </summary>
    /// <param name="objectId">The identifier of the object that owns the value.</param>
    /// <param name="valueId">The identifier of the referenced value.</param>
    private protected ValueUpdater(SerializationRecordId objectId, SerializationRecordId valueId)
    {
        ObjectId = objectId;
        ValueId = valueId;
    }

    /// <summary>
    ///  Gets the identifier of the referenced value.
    /// </summary>
    internal SerializationRecordId ValueId { get; }

    /// <summary>
    ///  Gets the identifier of the object that owns the value.
    /// </summary>
    internal SerializationRecordId ObjectId { get; }

    /// <summary>
    ///  Writes the resolved value back to its owning object.
    /// </summary>
    /// <param name="objects">The deserialized objects keyed by their record identifiers.</param>
    internal abstract void UpdateValue(IDictionary<SerializationRecordId, object> objects);
}