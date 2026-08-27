// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from dotnet/runtime at 7aa830a03599a8255c2c4abf2947afc5b346cc6f (MIT licensed):
// src/libraries/System.Resources.Extensions/src/System/Resources/Extensions/BinaryFormat/

using System.Formats.Nrbf;
using System.Runtime.Serialization;

namespace Touki.Resources.BinaryFormat;

/// <summary>
///  Replaces a named <see cref="SerializationInfo"/> member after its referenced value is available.
/// </summary>
internal sealed class SerializationInfoValueUpdater : ValueUpdater
{
    private readonly SerializationInfo _info;
    private readonly string _name;
    private readonly Type _serializedType;

    /// <summary>
    ///  Initializes an updater for a named serialization-information value.
    /// </summary>
    /// <param name="objectId">The identifier of the object described by the serialization information.</param>
    /// <param name="valueId">The identifier of the referenced value.</param>
    /// <param name="info">The serialization information to update.</param>
    /// <param name="name">The name of the value to update.</param>
    /// <param name="serializedType">
    ///  The serialized type to retain when the resolved value is <see langword="null"/>.
    /// </param>
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

    /// <inheritdoc/>
    internal override void UpdateValue(IDictionary<SerializationRecordId, object> objects)
    {
        object? newValue = objects[ValueId];
        _info.UpdateValue(_name, newValue, newValue?.GetType() ?? _serializedType);
    }
}