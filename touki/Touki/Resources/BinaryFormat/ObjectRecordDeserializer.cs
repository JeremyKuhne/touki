// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from dotnet/runtime at 7aa830a03599a8255c2c4abf2947afc5b346cc6f (MIT licensed):
// src/libraries/System.Resources.Extensions/src/System/Resources/Extensions/BinaryFormat/

using System.Formats.Nrbf;
using System.Runtime.Serialization;

namespace Touki.Resources.BinaryFormat;

#pragma warning disable SYSLIB0050 // Type or member is obsolete.

/// <summary>
///  Provides resumable member unwrapping and dependency detection for class and array record deserializers.
/// </summary>
internal abstract class ObjectRecordDeserializer
{
    /// <summary>
    ///  Represents a member value whose referenced object has not been materialized.
    /// </summary>
    private protected static readonly object s_missingValueSentinel = new();

    /// <summary>
    ///  Initializes a deserializer for an object record.
    /// </summary>
    /// <param name="objectRecord">The record to deserialize.</param>
    /// <param name="deserializer">The object-graph deserializer.</param>
    private protected ObjectRecordDeserializer(SerializationRecord objectRecord, IDeserializer deserializer)
    {
        Deserializer = deserializer;
        ObjectRecord = objectRecord;
    }

    /// <summary>
    ///  Gets the serialization record being deserialized.
    /// </summary>
    internal SerializationRecord ObjectRecord { get; }

    /// <summary>
    ///  Gets the object materialized from <see cref="ObjectRecord"/>.
    /// </summary>
    [AllowNull]
    internal object Object { get; private protected set; }

    /// <summary>
    ///  Gets the object-graph deserializer.
    /// </summary>
    private protected IDeserializer Deserializer { get; }

    /// <summary>
    ///  Continues deserialization until completion or an unresolved record is encountered.
    /// </summary>
    /// <returns>
    ///  The unresolved record identifier, or the <see langword="default"/> identifier when deserialization is complete.
    /// </returns>
    internal abstract SerializationRecordId Continue();

    /// <summary>
    ///  Resolves a serialized member value to its materialized value and record identifier.
    /// </summary>
    /// <param name="memberValue">The serialized member value.</param>
    /// <returns>The materialized value and its record identifier.</returns>
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

            if (value is not null)
            {
                ValidateNewMemberObjectValue(value);
            }

            return (value, id);
        }
    }

    /// <summary>
    ///  Validates a newly resolved object before it is assigned to a member.
    /// </summary>
    /// <param name="value">The resolved object to validate.</param>
    private protected virtual void ValidateNewMemberObjectValue(object value)
    {
    }

    /// <summary>
    ///  Determines whether a resolved value requires a later fixup.
    /// </summary>
    /// <param name="value">The resolved value.</param>
    /// <param name="valueRecord">The identifier of the value record.</param>
    /// <returns>
    ///  <see langword="true"/> if the value requires a later fixup; otherwise <see langword="false"/>.
    /// </returns>
    private protected bool DoesValueNeedUpdated(object value, SerializationRecordId valueRecord)
        => !valueRecord.Equals(default)
            && (value is IObjectReference
                || (Deserializer.IncompleteObjects.Contains(valueRecord) && value.GetType().IsValueType));

    /// <summary>
    ///  Creates the record-specific deserializer for a class or array record.
    /// </summary>
    /// <param name="record">The object record to deserialize.</param>
    /// <param name="deserializer">The object-graph deserializer.</param>
    /// <returns>The record-specific deserializer.</returns>
    internal static ObjectRecordDeserializer Create(SerializationRecord record, IDeserializer deserializer)
        => record switch
        {
            ClassRecord classRecord => ClassRecordDeserializer.Create(classRecord, deserializer),
            _ => new ArrayRecordDeserializer((ArrayRecord)record, deserializer)
        };
}

#pragma warning restore SYSLIB0050 // Type or member is obsolete.