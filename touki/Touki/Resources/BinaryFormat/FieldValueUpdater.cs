// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from dotnet/runtime at 7aa830a03599a8255c2c4abf2947afc5b346cc6f (MIT licensed):
// src/libraries/System.Resources.Extensions/src/System/Resources/Extensions/BinaryFormat/

using System.Formats.Nrbf;
using System.Reflection;

namespace Touki.Resources.BinaryFormat;

internal sealed class FieldValueUpdater : ValueUpdater
{
    private readonly FieldInfo _field;

    internal FieldValueUpdater(SerializationRecordId objectId, SerializationRecordId valueId, FieldInfo field)
        : base(objectId, valueId)
    {
        _field = field;
    }

    internal override void UpdateValue(IDictionary<SerializationRecordId, object> objects)
    {
        object newValue = objects[ValueId];
        _field.SetValue(objects[ObjectId], newValue);
    }
}