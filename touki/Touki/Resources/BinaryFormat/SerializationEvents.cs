// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from dotnet/runtime at 7aa830a03599a8255c2c4abf2947afc5b346cc6f (MIT licensed):
// src/libraries/System.Resources.Extensions/src/System/Resources/Extensions/BinaryFormat/

using System.Reflection;
using System.Runtime.Serialization;

namespace Touki.Resources.BinaryFormat;

internal sealed class SerializationEvents
{
    private static readonly SerializationEvents s_noEvents = new();

    private readonly List<MethodInfo>? _onDeserializingMethods;
    private readonly List<MethodInfo>? _onDeserializedMethods;

    private SerializationEvents()
    {
    }

    private SerializationEvents(
        List<MethodInfo>? onDeserializingMethods,
        List<MethodInfo>? onDeserializedMethods)
    {
        _onDeserializingMethods = onDeserializingMethods;
        _onDeserializedMethods = onDeserializedMethods;
    }

    internal static SerializationEvents Create(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
    {
        List<MethodInfo>? onDeserializingMethods = GetMethodsWithAttribute(
            typeof(OnDeserializingAttribute),
            type);
        List<MethodInfo>? onDeserializedMethods = GetMethodsWithAttribute(
            typeof(OnDeserializedAttribute),
            type);

        return onDeserializingMethods is null && onDeserializedMethods is null
            ? s_noEvents
            : new SerializationEvents(onDeserializingMethods, onDeserializedMethods);
    }

    private static List<MethodInfo>? GetMethodsWithAttribute(
        Type attribute,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type? type)
    {
        List<MethodInfo>? attributedMethods = null;

        Type? baseType = type;
        while (baseType is not null && baseType != typeof(object))
        {
            MethodInfo[] methods = baseType.GetMethods(
                BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            foreach (MethodInfo method in methods)
            {
                if (method.IsDefined(attribute, inherit: false))
                {
                    attributedMethods ??= [];
                    attributedMethods.Add(method);
                }
            }

            baseType = baseType.BaseType;
        }

        attributedMethods?.Reverse();
        return attributedMethods;
    }

    internal Action<StreamingContext>? GetOnDeserialized(object instance)
        => AddOnDelegate(instance, _onDeserializedMethods);

    internal Action<StreamingContext>? GetOnDeserializing(object instance)
        => AddOnDelegate(instance, _onDeserializingMethods);

    private static Action<StreamingContext>? AddOnDelegate(object instance, List<MethodInfo>? methods)
    {
        Action<StreamingContext>? handler = null;

        if (methods is not null)
        {
            foreach (MethodInfo method in methods)
            {
                Action<StreamingContext> callback =
#if NET
                    method.CreateDelegate<Action<StreamingContext>>(instance);
#else
                    (Action<StreamingContext>)method.CreateDelegate(typeof(Action<StreamingContext>), instance);
#endif
                handler += callback;
            }
        }

        return handler;
    }
}