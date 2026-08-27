// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from dotnet/runtime at 7aa830a03599a8255c2c4abf2947afc5b346cc6f (MIT licensed):
// src/libraries/System.Resources.Extensions/src/System/Resources/Extensions/BinaryFormat/

using System.Formats.Nrbf;
using System.Reflection;

namespace Touki.Resources.BinaryFormat;

#pragma warning disable SYSLIB0050 // Type or member is obsolete.

/// <summary>
///  Populates serializable fields from a class record and schedules fixups for incomplete member values.
/// </summary>
internal sealed class ClassRecordFieldInfoDeserializer : ClassRecordDeserializer
{
    private readonly ClassRecord _classRecord;
    private readonly MemberInfo[] _fieldInfo;
    private readonly bool _isValueType;
    private int _currentFieldIndex;
    private bool _hasFixups;

    /// <summary>
    ///  Initializes a deserializer that populates the serializable fields of a class instance.
    /// </summary>
    /// <param name="classRecord">The class record to deserialize.</param>
    /// <param name="instance">The object materialized for the record.</param>
    /// <param name="type">The resolved runtime type of <paramref name="instance"/>.</param>
    /// <param name="deserializer">The object-graph deserializer.</param>
    internal ClassRecordFieldInfoDeserializer(
        ClassRecord classRecord,
        object instance,
        Type type,
        IDeserializer deserializer)
        : base(classRecord, instance, deserializer)
    {
        _classRecord = classRecord;
#pragma warning disable IL2067 // FormatterServices.GetSerializableMembers is not attributed correctly.
        _fieldInfo = System.Runtime.Serialization.FormatterServices.GetSerializableMembers(type);
#pragma warning restore IL2067
        _isValueType = type.IsValueType;
    }

    /// <inheritdoc/>
    internal override SerializationRecordId Continue()
    {
        while (_currentFieldIndex < _fieldInfo.Length)
        {
            FieldInfo field = (FieldInfo)_fieldInfo[_currentFieldIndex];
            if (!_classRecord.HasMember(field.Name))
            {
                _currentFieldIndex++;
                continue;
            }

            (object? memberValue, SerializationRecordId reference) =
                UnwrapMemberValue(_classRecord.GetRawValue(field.Name));

            if (ReferenceEquals(s_missingValueSentinel, memberValue))
            {
                return reference;
            }

            field.SetValue(Object, memberValue);

            FieldValueUpdater? updater = null;
            if (memberValue is not null && !reference.Equals(default) && memberValue.GetType().IsValueType)
            {
                updater = new FieldValueUpdater(_classRecord.Id, reference, field);
                Deserializer.TrackValueTypeUpdater(updater);
            }

            if (memberValue is not null && DoesValueNeedUpdated(memberValue, reference))
            {
                _hasFixups = true;
                Deserializer.PendValueUpdater(
                    updater ?? new FieldValueUpdater(_classRecord.Id, reference, field));
            }

            _currentFieldIndex++;
        }

        if (!_hasFixups || !_isValueType)
        {
            Deserializer.CompleteObject(_classRecord.Id);
        }

        return default;
    }
}

#pragma warning restore SYSLIB0050 // Type or member is obsolete.