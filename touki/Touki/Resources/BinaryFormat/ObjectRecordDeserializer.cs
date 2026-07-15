// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from dotnet/runtime at 7aa830a03599a8255c2c4abf2947afc5b346cc6f (MIT licensed):
// src/libraries/System.Resources.Extensions/src/System/Resources/Extensions/BinaryFormat/

using System.Formats.Nrbf;
using System.Runtime.Serialization;

namespace Touki.Resources.BinaryFormat;

#pragma warning disable SYSLIB0050 // Type or member is obsolete.

internal abstract class ObjectRecordDeserializer
{
    private protected static readonly object s_missingValueSentinel = new();

    private protected ObjectRecordDeserializer(SerializationRecord objectRecord, IDeserializer deserializer)
    {
        Deserializer = deserializer;
        ObjectRecord = objectRecord;
    }

    internal SerializationRecord ObjectRecord { get; }

    [AllowNull]
    internal object Object { get; private protected set; }

    private protected IDeserializer Deserializer { get; }

    internal abstract SerializationRecordId Continue();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected (object? value, SerializationRecordId id) UnwrapMemberValue(object? memberValue)
    {
        if (memberValue is null)
        {
            return (null, default);
        }

        if (memberValue is not SerializationRecord serializationRecord)
        {
            return (memberValue, default);
        }

        if (serializationRecord.RecordType is SerializationRecordType.BinaryObjectString)
        {
            PrimitiveTypeRecord<string> stringRecord = (PrimitiveTypeRecord<string>)serializationRecord;
            return (stringRecord.Value, stringRecord.Id);
        }

        if (serializationRecord.RecordType is SerializationRecordType.MemberPrimitiveTyped)
        {
            return (((PrimitiveTypeRecord)serializationRecord).Value, default);
        }

        return TryGetObject(serializationRecord.Id);

        (object? value, SerializationRecordId id) TryGetObject(SerializationRecordId id)
        {
            if (!Deserializer.DeserializedObjects.TryGetValue(id, out object? value))
            {
                return (s_missingValueSentinel, id);
            }

            ValidateNewMemberObjectValue(value);
            return (value, id);
        }
    }

    private protected virtual void ValidateNewMemberObjectValue(object value)
    {
    }

    private protected bool DoesValueNeedUpdated(object value, SerializationRecordId valueRecord)
        => !valueRecord.Equals(default)
            && (value is IObjectReference
                || (Deserializer.IncompleteObjects.Contains(valueRecord) && value.GetType().IsValueType));

    internal static ObjectRecordDeserializer Create(SerializationRecord record, IDeserializer deserializer)
        => record switch
        {
            ClassRecord classRecord => ClassRecordDeserializer.Create(classRecord, deserializer),
            _ => new ArrayRecordDeserializer((ArrayRecord)record, deserializer)
        };
}

#pragma warning restore SYSLIB0050 // Type or member is obsolete.