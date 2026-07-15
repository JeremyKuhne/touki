// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from dotnet/runtime at 7aa830a03599a8255c2c4abf2947afc5b346cc6f (MIT licensed):
// src/libraries/System.Resources.Extensions/src/System/Resources/Extensions/BinaryFormat/

using System.Formats.Nrbf;
using System.Runtime.Serialization;

namespace Touki.Resources.BinaryFormat;

#pragma warning disable SYSLIB0050 // Type or member is obsolete.

internal abstract class ClassRecordDeserializer : ObjectRecordDeserializer
{
    private readonly bool _onlyAllowPrimitives;

    private protected ClassRecordDeserializer(
        ClassRecord classRecord,
        object instance,
        IDeserializer deserializer)
        : base(classRecord, deserializer)
    {
        Object = instance;
        _onlyAllowPrimitives = instance is IObjectReference;
    }

    internal static ObjectRecordDeserializer Create(ClassRecord classRecord, IDeserializer deserializer)
    {
        Type type = deserializer.TypeResolver.BindToType(classRecord.TypeName);
        if (!type.IsSerializable)
        {
            throw new SerializationException($"Type '{type}' is not marked as serializable.");
        }

        object instance =
#if NET
            RuntimeHelpers.GetUninitializedObject(type);
#else
            System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
#endif

        deserializer.GetSerializationEvents(type).GetOnDeserializing(instance)?.Invoke(deserializer.StreamingContext);

        return typeof(ISerializable).IsAssignableFrom(type)
            ? new ClassRecordSerializationInfoDeserializer(classRecord, instance, type, deserializer)
            : new ClassRecordFieldInfoDeserializer(classRecord, instance, type, deserializer);
    }

    private protected override void ValidateNewMemberObjectValue(object value)
    {
        if (!_onlyAllowPrimitives)
        {
            return;
        }

        Type type = value.GetType();
        if (type.IsArray)
        {
            type = type.GetElementType()!;
        }

        if (!type.IsPrimitive && !type.IsEnum && type != typeof(string))
        {
            throw new SerializationException(
                $"IObjectReference type '{Object.GetType()}' can only contain primitive values, not '{type}'.");
        }
    }
}

#pragma warning restore SYSLIB0050 // Type or member is obsolete.