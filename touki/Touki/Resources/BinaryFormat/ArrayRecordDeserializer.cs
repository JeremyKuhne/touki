// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from dotnet/runtime at 7aa830a03599a8255c2c4abf2947afc5b346cc6f (MIT licensed):
// src/libraries/System.Resources.Extensions/src/System/Resources/Extensions/BinaryFormat/

using System.Formats.Nrbf;

namespace Touki.Resources.BinaryFormat;

internal sealed class ArrayRecordDeserializer : ObjectRecordDeserializer
{
    private readonly ArrayRecord _arrayRecord;
    private readonly Type _elementType;
    private readonly Array _arrayOfClassRecords;
    private readonly Array _arrayOfT;
    private readonly int[] _lengths;
    private readonly int[] _indices;
    private bool _hasFixups;
    private bool _canIterate;

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling",
        Justification = "The exact array type is supplied by a generic registration and is therefore available to AOT.")]
    internal ArrayRecordDeserializer(ArrayRecord arrayRecord, IDeserializer deserializer)
        : base(arrayRecord, deserializer)
    {
        Debug.Assert(
            arrayRecord.RecordType is not
                (SerializationRecordType.ArraySingleString or SerializationRecordType.ArraySinglePrimitive));

        _arrayRecord = arrayRecord;
        Type expectedArrayType = deserializer.TypeResolver.BindToType(arrayRecord.TypeName);
        _elementType = expectedArrayType.GetElementType()!;

        _arrayOfClassRecords = arrayRecord.GetArray(expectedArrayType);
        _lengths = arrayRecord.Lengths.ToArray();
        Object = _arrayOfT = Array.CreateInstance(_elementType, _lengths);
        _indices = new int[_lengths.Length];
        _canIterate = _arrayOfT.Length > 0;
    }

    internal override SerializationRecordId Continue()
    {
        int[] indices = _indices;
        int[] lengths = _lengths;

        while (_canIterate)
        {
            (object? memberValue, SerializationRecordId reference) =
                UnwrapMemberValue(_arrayOfClassRecords.GetValue(indices));

            if (ReferenceEquals(s_missingValueSentinel, memberValue))
            {
                return reference;
            }

            _arrayOfT.SetValue(memberValue, indices);

            ArrayUpdater? updater = null;
            if (memberValue is not null && !reference.Equals(default) && memberValue.GetType().IsValueType)
            {
                updater = new ArrayUpdater(_arrayRecord.Id, reference, [.. indices]);
                Deserializer.TrackValueTypeUpdater(updater);
            }

            if (memberValue is not null && DoesValueNeedUpdated(memberValue, reference))
            {
                _hasFixups = true;
                Deserializer.PendValueUpdater(updater ?? new ArrayUpdater(_arrayRecord.Id, reference, [.. indices]));
            }

            int dimension = indices.Length - 1;
            while (dimension >= 0)
            {
                indices[dimension]++;
                if (indices[dimension] < lengths[dimension])
                {
                    break;
                }

                indices[dimension] = 0;
                dimension--;
            }

            if (dimension < 0)
            {
                _canIterate = false;
            }
        }

        if (!_hasFixups)
        {
            Deserializer.CompleteObject(_arrayRecord.Id);
        }

        return default;
    }

    internal static Array GetArraySinglePrimitive(SerializationRecord record)
        => record switch
        {
            SZArrayRecord<bool> primitiveArray => primitiveArray.GetArray(),
            SZArrayRecord<byte> primitiveArray => primitiveArray.GetArray(),
            SZArrayRecord<sbyte> primitiveArray => primitiveArray.GetArray(),
            SZArrayRecord<char> primitiveArray => primitiveArray.GetArray(),
            SZArrayRecord<short> primitiveArray => primitiveArray.GetArray(),
            SZArrayRecord<ushort> primitiveArray => primitiveArray.GetArray(),
            SZArrayRecord<int> primitiveArray => primitiveArray.GetArray(),
            SZArrayRecord<uint> primitiveArray => primitiveArray.GetArray(),
            SZArrayRecord<long> primitiveArray => primitiveArray.GetArray(),
            SZArrayRecord<ulong> primitiveArray => primitiveArray.GetArray(),
            SZArrayRecord<float> primitiveArray => primitiveArray.GetArray(),
            SZArrayRecord<double> primitiveArray => primitiveArray.GetArray(),
            SZArrayRecord<decimal> primitiveArray => primitiveArray.GetArray(),
            SZArrayRecord<DateTime> primitiveArray => primitiveArray.GetArray(),
            SZArrayRecord<TimeSpan> primitiveArray => primitiveArray.GetArray(),
            _ => throw new NotSupportedException()
        };

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling",
        Justification = "The exact array type is supplied by a generic registration and is therefore available to AOT.")]
    internal static Array? GetRectangularArrayOfPrimitives(
        ArrayRecord arrayRecord,
        ITypeResolver typeResolver)
    {
        if (arrayRecord.Rank <= 1 || arrayRecord.TypeName.GetElementType().IsArray)
        {
            return null;
        }

        Type expectedArrayType = typeResolver.BindToType(arrayRecord.TypeName);
        Type elementType = expectedArrayType.GetElementType()!;
        while (elementType.IsArray)
        {
            elementType = elementType.GetElementType()!;
        }

        if (!HasBuiltInSupport(elementType))
        {
            return null;
        }

        return arrayRecord.GetArray(expectedArrayType);

        static bool HasBuiltInSupport(Type type)
            => type == typeof(string)
            || type == typeof(bool)
            || type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(char)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(TimeSpan);
    }
}