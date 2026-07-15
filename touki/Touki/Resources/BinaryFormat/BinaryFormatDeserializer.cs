// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from dotnet/runtime at 7aa830a03599a8255c2c4abf2947afc5b346cc6f (MIT licensed):
// src/libraries/System.Resources.Extensions/src/System/Resources/Extensions/BinaryFormat/

using System.Formats.Nrbf;
using System.Runtime.Serialization;

namespace Touki.Resources.BinaryFormat;

#pragma warning disable SYSLIB0050 // Type or member is obsolete.

internal sealed class BinaryFormatDeserializer : IDeserializer
{
    private const int InitialParserStackCapacity = 8;

    // Do not reserve in proportion to a record map that may contain many unreachable records.
    private const int MaximumCapacityHint = 256;

    private readonly IReadOnlyDictionary<SerializationRecordId, SerializationRecord> _recordMap;
    private readonly ITypeResolver _typeResolver;
    private readonly StreamingContext _streamingContext;
    private readonly Dictionary<SerializationRecordId, object> _deserializedObjects = [];
    private readonly HashSet<SerializationRecordId> _incompleteObjects = [];
    private readonly Queue<SerializationRecordId> _pendingCompletions = [];
    private readonly SerializationRecordId _rootId;

    private Queue<PendingSerializationInfo>? _pendingSerializationInfo;
    private HashSet<SerializationRecordId>? _pendingSerializationInfoIds;
    private Dictionary<SerializationRecordId, HashSet<SerializationRecordId>>? _incompleteDependencies;
    private HashSet<ValueUpdater>? _pendingUpdates;
    private Dictionary<SerializationRecordId, List<ValueUpdater>>? _valueTypeUpdaters;
    private Dictionary<Type, SerializationEvents>? _serializationEvents;
    private List<(SerializationRecordId Id, Action<StreamingContext> Callback)>? _onDeserializedCallbacks;
    private List<(SerializationRecordId Id, IDeserializationCallback Callback)>? _onDeserializationCallbacks;

    private BinaryFormatDeserializer(
        SerializationRecordId rootId,
        IReadOnlyDictionary<SerializationRecordId, SerializationRecord> recordMap,
        ITypeResolver typeResolver,
        StreamingContext streamingContext)
    {
        _rootId = rootId;
        _recordMap = recordMap;
        _typeResolver = typeResolver;
        _streamingContext = streamingContext;
    }

    StreamingContext IDeserializer.StreamingContext => _streamingContext;

    HashSet<SerializationRecordId> IDeserializer.IncompleteObjects => _incompleteObjects;

    IDictionary<SerializationRecordId, object> IDeserializer.DeserializedObjects => _deserializedObjects;

    ITypeResolver IDeserializer.TypeResolver => _typeResolver;

    SerializationEvents IDeserializer.GetSerializationEvents(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
    {
        if (_typeResolver is RegisteredTypeResolver registeredTypeResolver)
        {
            return registeredTypeResolver.GetSerializationEvents(type);
        }

        _serializationEvents ??= [];
        if (!_serializationEvents.TryGetValue(type, out SerializationEvents? events))
        {
            events = SerializationEvents.Create(type);
            _serializationEvents.Add(type, events);
        }

        return events;
    }

    internal static object Deserialize(
        SerializationRecordId rootId,
        IReadOnlyDictionary<SerializationRecordId, SerializationRecord> recordMap,
        ITypeResolver typeResolver,
        StreamingContext streamingContext)
    {
        BinaryFormatDeserializer deserializer = new(rootId, recordMap, typeResolver, streamingContext);
        return deserializer.Deserialize();
    }

    private object Deserialize()
    {
        DeserializeRoot(_rootId);

        int pendingCount = _pendingSerializationInfo?.Count ?? 0;
        while (_pendingSerializationInfo is not null && _pendingSerializationInfo.Count > 0)
        {
            PendingSerializationInfo pending = _pendingSerializationInfo.Dequeue();

            if (--pendingCount >= 0
                && _pendingSerializationInfo.Count != 0
                && _incompleteDependencies is not null
                && _incompleteDependencies.TryGetValue(
                    pending.ObjectId,
                    out HashSet<SerializationRecordId>? dependencies))
            {
                if (dependencies.Count > 0)
                {
                    _pendingSerializationInfo.Enqueue(pending);
                    continue;
                }

                Debug.Fail("Completed dependencies should have been removed from the dictionary.");
            }

            pending.Populate(_deserializedObjects, _streamingContext);
            _pendingSerializationInfoIds?.Remove(pending.ObjectId);
            ((IDeserializer)this).CompleteObject(pending.ObjectId);
        }

        if (_incompleteObjects.Count > 0 || (_pendingUpdates is not null && _pendingUpdates.Count > 0))
        {
            throw new SerializationException("The serialized object graph could not be completed.");
        }

        if (_onDeserializedCallbacks is not null)
        {
            foreach ((SerializationRecordId id, Action<StreamingContext> callback) in _onDeserializedCallbacks)
            {
                callback(_streamingContext);
                ReapplyValueType(id);
            }
        }

        if (_onDeserializationCallbacks is not null)
        {
            foreach ((SerializationRecordId id, IDeserializationCallback callback) in _onDeserializationCallbacks)
            {
                callback.OnDeserialization(null);
                ReapplyValueType(id);
            }
        }

        return _deserializedObjects[_rootId];
    }

    private void DeserializeRoot(SerializationRecordId rootId)
    {
        object root = DeserializeNew(rootId);
        if (root is not ObjectRecordDeserializer parser)
        {
            return;
        }

        Stack<ObjectRecordDeserializer> parserStack = new(capacity: InitialParserStackCapacity);
        parserStack.Push(parser);
#if NET
        bool parserStackCapacityHintApplied = false;
#endif

        while (parserStack.Count > 0)
        {
            ObjectRecordDeserializer currentParser = parserStack.Pop();

            SerializationRecordId requiredId;
            while (!(requiredId = currentParser.Continue()).Equals(default))
            {
                if (DeserializeNew(requiredId) is ObjectRecordDeserializer requiredParser)
                {
#if NET
                    if (!parserStackCapacityHintApplied
                        && parserStack.Count >= InitialParserStackCapacity - 1
                        && _recordMap.Count > InitialParserStackCapacity)
                    {
                        parserStack.EnsureCapacity(GetCapacityHint(_recordMap.Count));
                        parserStackCapacityHintApplied = true;
                    }
#endif

                    parserStack.Push(currentParser);
                    parserStack.Push(requiredParser);
                    break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        object DeserializeNew(SerializationRecordId id)
        {
            SerializationRecord record = _recordMap[id];

            object? value = record.RecordType switch
            {
                SerializationRecordType.BinaryObjectString => ((PrimitiveTypeRecord<string>)record).Value,
                SerializationRecordType.MemberPrimitiveTyped => ((PrimitiveTypeRecord)record).Value,
                SerializationRecordType.ArraySingleString => ((SZArrayRecord<string>)record).GetArray(),
                SerializationRecordType.ArraySinglePrimitive =>
                    ArrayRecordDeserializer.GetArraySinglePrimitive(record),
                SerializationRecordType.BinaryArray =>
                    ArrayRecordDeserializer.GetRectangularArrayOfPrimitives((ArrayRecord)record, _typeResolver),
                _ => null
            };

            if (value is not null)
            {
                AddDeserializedObject(record.Id, value);
                return value;
            }

            if (!_incompleteObjects.Add(id))
            {
                throw new SerializationException("The serialized object graph contains an unsupported cycle.");
            }

            ObjectRecordDeserializer recordDeserializer = ObjectRecordDeserializer.Create(record, this);
            AddDeserializedObject(id, recordDeserializer.Object);
            return recordDeserializer;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddDeserializedObject(SerializationRecordId id, object value)
    {
#if NET
        if (_deserializedObjects.Count == 7 && _recordMap.Count > 7)
        {
            _deserializedObjects.EnsureCapacity(GetCapacityHint(_recordMap.Count));
        }
#endif

        _deserializedObjects.Add(id, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetCapacityHint(int count) => Math.Min(count, MaximumCapacityHint);

    void IDeserializer.PendSerializationInfo(PendingSerializationInfo pending)
    {
        _pendingSerializationInfo ??= new();
        _pendingSerializationInfo.Enqueue(pending);
        _pendingSerializationInfoIds ??= [];
        _pendingSerializationInfoIds.Add(pending.ObjectId);
    }

    void IDeserializer.PendValueUpdater(ValueUpdater updater)
    {
        _pendingUpdates ??= [];
        _pendingUpdates.Add(updater);

        _incompleteDependencies ??= [];

        if (_incompleteDependencies.TryGetValue(
            updater.ObjectId,
            out HashSet<SerializationRecordId>? dependencies))
        {
            dependencies.Add(updater.ValueId);
        }
        else
        {
            _incompleteDependencies.Add(updater.ObjectId, [updater.ValueId]);
        }
    }

    void IDeserializer.TrackValueTypeUpdater(ValueUpdater updater)
    {
        _valueTypeUpdaters ??= [];
        if (!_valueTypeUpdaters.TryGetValue(updater.ValueId, out List<ValueUpdater>? updaters))
        {
            updaters = [];
            _valueTypeUpdaters.Add(updater.ValueId, updaters);
        }

        updaters.Add(updater);
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2072:'type' argument does not satisfy 'DynamicallyAccessedMemberTypes.All'",
        Justification = "The instance was created from the fully annotated type returned by ITypeResolver.BindToType.")]
    void IDeserializer.CompleteObject(SerializationRecordId id)
    {
        _pendingCompletions.Enqueue(id);

        while (_pendingCompletions.Count > 0)
        {
            SerializationRecordId completedId = _pendingCompletions.Dequeue();
            _incompleteObjects.Remove(completedId);

            if (_incompleteDependencies is not null
                && _incompleteDependencies.TryGetValue(
                    completedId,
                    out HashSet<SerializationRecordId>? completedDependencies)
                && completedDependencies.Count == 0)
            {
                _incompleteDependencies.Remove(completedId);

                if (_pendingSerializationInfoIds is not null
                    && _pendingSerializationInfoIds.Contains(completedId))
                {
                    continue;
                }
            }

            if (_recordMap[completedId] is ClassRecord
                && (_incompleteDependencies is null || !_incompleteDependencies.ContainsKey(completedId)))
            {
                object instance = _deserializedObjects[completedId];
                Type type = instance.GetType();

                Action<StreamingContext>? onDeserialized = ((IDeserializer)this)
                    .GetSerializationEvents(type)
                    .GetOnDeserialized(instance);
                if (onDeserialized is not null)
                {
                    _onDeserializedCallbacks ??= [];
                    _onDeserializedCallbacks.Add((completedId, onDeserialized));
                }

                if (instance is IDeserializationCallback callback)
                {
                    _onDeserializationCallbacks ??= [];
                    _onDeserializationCallbacks.Add((completedId, callback));
                }

                if (instance is IObjectReference objectReference)
                {
                    _deserializedObjects[completedId] = objectReference.GetRealObject(_streamingContext);
                }
            }

            if (_incompleteDependencies is null)
            {
                continue;
            }

            Debug.Assert(_pendingUpdates is not null);

            foreach (KeyValuePair<SerializationRecordId, HashSet<SerializationRecordId>> pair
                in _incompleteDependencies)
            {
                SerializationRecordId incompleteId = pair.Key;
                HashSet<SerializationRecordId> dependencies = pair.Value;

                if (!dependencies.Remove(completedId))
                {
                    continue;
                }

                _pendingUpdates!.RemoveWhere((ValueUpdater updater) =>
                {
                    if (!updater.ValueId.Equals(completedId))
                    {
                        return false;
                    }

                    updater.UpdateValue(_deserializedObjects);
                    return true;
                });

                if (dependencies.Count != 0)
                {
                    continue;
                }

                _pendingCompletions.Enqueue(incompleteId);
            }
        }
    }

    private void ReapplyValueType(SerializationRecordId id)
        => ReapplyValueType(id, []);

    private void ReapplyValueType(SerializationRecordId id, HashSet<SerializationRecordId> visited)
    {
        if (!visited.Add(id))
        {
            return;
        }

        if (_valueTypeUpdaters is not null
            && _valueTypeUpdaters.TryGetValue(id, out List<ValueUpdater>? updaters))
        {
            foreach (ValueUpdater updater in updaters)
            {
                updater.UpdateValue(_deserializedObjects);

                if (_deserializedObjects[updater.ObjectId].GetType().IsValueType)
                {
                    ReapplyValueType(updater.ObjectId, visited);
                }
            }
        }

        visited.Remove(id);
    }
}

#pragma warning restore SYSLIB0050 // Type or member is obsolete.