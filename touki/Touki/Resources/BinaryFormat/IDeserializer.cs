// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from dotnet/runtime at 7aa830a03599a8255c2c4abf2947afc5b346cc6f (MIT licensed):
// src/libraries/System.Resources.Extensions/src/System/Resources/Extensions/BinaryFormat/

using System.Formats.Nrbf;
using System.Runtime.Serialization;

namespace Touki.Resources.BinaryFormat;

#pragma warning disable SYSLIB0050 // Type or member is obsolete.

internal interface IDeserializer
{
    StreamingContext StreamingContext { get; }

    HashSet<SerializationRecordId> IncompleteObjects { get; }

    IDictionary<SerializationRecordId, object> DeserializedObjects { get; }

    ITypeResolver TypeResolver { get; }

    SerializationEvents GetSerializationEvents(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type);

    void PendValueUpdater(ValueUpdater updater);

    void TrackValueTypeUpdater(ValueUpdater updater);

    void PendSerializationInfo(PendingSerializationInfo pending);

    void CompleteObject(SerializationRecordId id);
}

#pragma warning restore SYSLIB0050 // Type or member is obsolete.