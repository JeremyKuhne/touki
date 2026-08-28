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
///  Defines the object-graph state and fixup scheduling shared by record-specific deserializers.
/// </summary>
internal interface IDeserializer
{
    /// <summary>
    ///  Gets the context supplied to serialization callbacks and constructors.
    /// </summary>
    StreamingContext StreamingContext { get; }

    /// <summary>
    ///  Gets the identifiers of objects whose deserialization has not completed.
    /// </summary>
    HashSet<SerializationRecordId> IncompleteObjects { get; }

    /// <summary>
    ///  Gets the materialized objects keyed by their serialization record identifiers.
    /// </summary>
    IDictionary<SerializationRecordId, object> DeserializedObjects { get; }

    /// <summary>
    ///  Gets the resolver used to bind serialized type names to runtime types.
    /// </summary>
    ITypeResolver TypeResolver { get; }

    /// <summary>
    ///  Gets the serialization callbacks declared by the specified type.
    /// </summary>
    /// <param name="type">The type whose serialization callbacks are requested.</param>
    /// <returns>The callbacks declared by <paramref name="type"/>.</returns>
    SerializationEvents GetSerializationEvents(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type);

    /// <summary>
    ///  Schedules a value updater until its referenced value is complete.
    /// </summary>
    /// <param name="updater">The updater to schedule.</param>
    void PendValueUpdater(ValueUpdater updater);

    /// <summary>
    ///  Tracks a value-type updater for later reapplication.
    /// </summary>
    /// <param name="updater">The updater to track.</param>
    void TrackValueTypeUpdater(ValueUpdater updater);

    /// <summary>
    ///  Queues delayed population of serialization information.
    /// </summary>
    /// <param name="pending">The pending serialization information.</param>
    void PendSerializationInfo(PendingSerializationInfo pending);

    /// <summary>
    ///  Marks an object as complete and applies dependent fixups.
    /// </summary>
    /// <param name="id">The identifier of the completed object.</param>
    void CompleteObject(SerializationRecordId id);
}

#pragma warning restore SYSLIB0050 // Type or member is obsolete.