// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from dotnet/runtime at 7aa830a03599a8255c2c4abf2947afc5b346cc6f (MIT licensed):
// src/libraries/System.Resources.Extensions/src/System/Resources/Extensions/BinaryFormat/

using System.Formats.Nrbf;
using System.Runtime.Serialization;

namespace Touki.Resources.BinaryFormat;

internal sealed class SerializationInfoValueUpdater : ValueUpdater
{
    private readonly SerializationInfo _info;
    private readonly string _name;
    private readonly Type _serializedType;

    internal SerializationInfoValueUpdater(
        SerializationRecordId objectId,
        SerializationRecordId valueId,
        SerializationInfo info,
        string name,
        Type serializedType)
        : base(objectId, valueId)
    {
        _info = info;
        _name = name;
        _serializedType = serializedType;
    }

    internal override void UpdateValue(IDictionary<SerializationRecordId, object> objects)
    {
        object? newValue = objects[ValueId];
        _info.UpdateValue(_name, newValue, newValue?.GetType() ?? _serializedType);
    }
}