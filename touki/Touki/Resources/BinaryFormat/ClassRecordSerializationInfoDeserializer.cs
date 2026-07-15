// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from dotnet/runtime at 7aa830a03599a8255c2c4abf2947afc5b346cc6f (MIT licensed):
// src/libraries/System.Resources.Extensions/src/System/Resources/Extensions/BinaryFormat/

using System.Formats.Nrbf;
using System.Runtime.Serialization;

namespace Touki.Resources.BinaryFormat;

#pragma warning disable SYSLIB0050 // Type or member is obsolete.

internal sealed class ClassRecordSerializationInfoDeserializer : ClassRecordDeserializer
{
    private readonly ClassRecord _classRecord;
    private readonly SerializationInfo _serializationInfo;

    // MemberNames is exposed only as IEnumerable<string>. Retain one iterator so traversal can resume after resolving
    // a dependency without copying the names or restarting enumeration.
    private readonly IEnumerator<string> _memberNamesIterator;

    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
    private readonly Type _type;

    private bool _canIterate;

    internal ClassRecordSerializationInfoDeserializer(
        ClassRecord classRecord,
        object instance,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
        Type type,
        IDeserializer deserializer)
        : base(classRecord, instance, deserializer)
    {
        _classRecord = classRecord;
        _type = type;
        _serializationInfo = new(type, BinaryFormattedObject.DefaultConverter);
        _memberNamesIterator = _classRecord.MemberNames.GetEnumerator();
        _canIterate = _memberNamesIterator.MoveNext();
    }

    internal override SerializationRecordId Continue()
    {
        if (_canIterate)
        {
            do
            {
                string memberName = _memberNamesIterator.Current;
                (object? memberValue, SerializationRecordId reference) =
                    UnwrapMemberValue(_classRecord.GetRawValue(memberName));

                if (ReferenceEquals(s_missingValueSentinel, memberValue))
                {
                    return reference;
                }

                if (memberValue is not null && DoesValueNeedUpdated(memberValue, reference))
                {
                    Deserializer.PendValueUpdater(new SerializationInfoValueUpdater(
                        _classRecord.Id,
                        reference,
                        _serializationInfo,
                        memberName,
                        memberValue.GetType()));
                }

                _serializationInfo.AddValue(memberName, memberValue);
            }
            while (_memberNamesIterator.MoveNext());

            _canIterate = false;
        }

        Deserializer.PendSerializationInfo(new PendingSerializationInfo(
            _classRecord.Id,
            _serializationInfo,
            _type));

        return default;
    }
}

#pragma warning restore SYSLIB0050 // Type or member is obsolete.