// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from dotnet/runtime at 7aa830a03599a8255c2c4abf2947afc5b346cc6f (MIT licensed):
// src/libraries/System.Resources.Extensions/src/System/Resources/Extensions/BinaryFormat/

using System.Formats.Nrbf;
using System.Reflection;
using System.Runtime.Serialization;

namespace Touki.Resources.BinaryFormat;

#pragma warning disable SYSLIB0050 // Type or member is obsolete.

/// <summary>
///  Defers invoking a type's serialization constructor until its <see cref="SerializationInfo"/> is complete.
/// </summary>
internal sealed class PendingSerializationInfo
{
    private readonly SerializationInfo _info;

    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
    private readonly Type _type;

    /// <summary>
    ///  Initializes pending serialization-constructor work for an object.
    /// </summary>
    /// <param name="objectId">The identifier of the object to populate.</param>
    /// <param name="info">The completed serialization information.</param>
    /// <param name="type">The runtime type that declares the serialization constructor.</param>
    internal PendingSerializationInfo(
        SerializationRecordId objectId,
        SerializationInfo info,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
        Type type)
    {
        ObjectId = objectId;
        _info = info;
        _type = type;
    }

    /// <summary>
    ///  Gets the identifier of the object to populate.
    /// </summary>
    internal SerializationRecordId ObjectId { get; }

    /// <summary>
    ///  Invokes the serialization constructor on the pending object.
    /// </summary>
    /// <param name="objects">The deserialized objects keyed by their record identifiers.</param>
    /// <param name="context">The context supplied to the serialization constructor.</param>
    internal void Populate(IDictionary<SerializationRecordId, object> objects, StreamingContext context)
    {
        object instance = objects[ObjectId];
        ConstructorInfo constructor = GetDeserializationConstructor(_type);
        constructor.Invoke(instance, [_info, context]);
    }

    private static ConstructorInfo GetDeserializationConstructor(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
        Type type)
    {
        foreach (ConstructorInfo constructor in type.GetConstructors(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            ParameterInfo[] parameters = constructor.GetParameters();
            if (parameters.Length == 2
                && parameters[0].ParameterType == typeof(SerializationInfo)
                && parameters[1].ParameterType == typeof(StreamingContext))
            {
                return constructor;
            }
        }

        throw new SerializationException(
            $"Type '{type.FullName}' does not have a serialization constructor.");
    }
}

#pragma warning restore SYSLIB0050 // Type or member is obsolete.