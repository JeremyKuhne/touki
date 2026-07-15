// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from dotnet/runtime at 7aa830a03599a8255c2c4abf2947afc5b346cc6f (MIT licensed):
// src/libraries/System.Resources.Extensions/src/System/Resources/Extensions/BinaryFormat/

using System.Reflection;
using System.Runtime.Serialization;

namespace Touki.Resources.BinaryFormat;

internal static class SerializationInfoExtensions
{
    private static readonly Action<SerializationInfo, string, object, Type> s_updateValue =
#if NET
        typeof(SerializationInfo)
            .GetMethod("UpdateValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .CreateDelegate<Action<SerializationInfo, string, object, Type>>();
#else
        (Action<SerializationInfo, string, object, Type>)typeof(SerializationInfo)
            .GetMethod("UpdateValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .CreateDelegate(typeof(Action<SerializationInfo, string, object, Type>));
#endif

    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicMethods, typeof(SerializationInfo))]
    internal static void UpdateValue(this SerializationInfo info, string name, object value, Type type)
        => s_updateValue(info, name, value, type);
}