// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from dotnet/runtime at 7aa830a03599a8255c2c4abf2947afc5b346cc6f (MIT licensed):
// src/libraries/System.Resources.Extensions/src/System/Resources/Extensions/BinaryFormat/

using System.Formats.Nrbf;

namespace Touki.Resources.BinaryFormat;

internal abstract class ValueUpdater
{
    private protected ValueUpdater(SerializationRecordId objectId, SerializationRecordId valueId)
    {
        ObjectId = objectId;
        ValueId = valueId;
    }

    internal SerializationRecordId ValueId { get; }

    internal SerializationRecordId ObjectId { get; }

    internal abstract void UpdateValue(IDictionary<SerializationRecordId, object> objects);
}