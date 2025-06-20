// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Concurrent;
using System.Reflection;

namespace System;

public static unsafe partial class EnumExtensions
{
    private static readonly ConcurrentDictionary<Type, EnumData> s_enumData = new();

    [ThreadStatic]
    private static object[]? t_params;

    private static readonly MethodInfo s_cachedNames = typeof(Enum).GetMethod(
        "GetCachedValuesAndNames",
        BindingFlags.NonPublic | BindingFlags.Static) ?? throw new InvalidOperationException();

    private static readonly Type s_valuesAndNames = typeof(Enum).GetNestedType(
        "ValuesAndNames",
        BindingFlags.NonPublic)!;

    private static readonly FieldInfo s_valuesField = s_valuesAndNames.GetField(
        "Values",
        BindingFlags.Public | BindingFlags.Instance)!;

    private static readonly FieldInfo s_namesField = s_valuesAndNames.GetField(
        "Names",
        BindingFlags.Public | BindingFlags.Instance)!;

    /// <inheritdoc cref="GetValuesAndNames(Type)"/>
    public static (ulong[] Values, string[] Names) GetValuesAndNames<T>() where T : Enum
        => GetValuesAndNames(typeof(T));

    /// <summary>
    ///  Gets the internal cached values and names for the specified enum type.
    /// </summary>
    public static (ulong[] Values, string[]) GetValuesAndNames(Type type)
    {
        if (!type.IsEnum)
        {
            throw new ArgumentException("Type must be an enum.", nameof(type));
        }

        // Use reflection to get the private static GetCachedValuesAndNames method
        t_params ??= [null!, true];
        var parameters = t_params;
        parameters[0] = type;
        var valuesAndNames = s_cachedNames.Invoke(null, parameters);
        var values = (ulong[])s_valuesField.GetValue(valuesAndNames)!;
        var names = (string[])s_namesField.GetValue(valuesAndNames)!;
        return (values, names);
    }

    /// <summary>
    ///  Gets cached data for an enum type, including its values, names, and whether it is a flags enum.
    /// </summary>
    public static EnumData GetEnumData(Type type) => s_enumData.GetOrAdd(type, t => new EnumData(t));

    /// <summary>
    ///  Cached data for an enum type, including its values, names, and whether it is a flags enum.
    /// </summary>
    public class EnumData
    {
        /// <summary>
        ///  Initializes a new instance of the <see cref="EnumData"/> class.
        /// </summary>
        public EnumData(Type type)
        {
            if (!type.IsEnum)
            {
                throw new ArgumentException("Type must be an enum.", nameof(type));
            }

            Type = type;
            Data = GetValuesAndNames(type);
            IsFlags = type.IsDefined(typeof(FlagsAttribute), inherit: false);
            UnderlyingType = type.GetEnumUnderlyingType();
        }

        /// <summary>
        ///  Type of the enum.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        ///  Data for the enum, including values and names.
        /// </summary>
        public (ulong[] Values, string[] Names) Data { get; }

        /// <summary>
        ///  Whether the enum is a flags enum.
        /// </summary>
        public bool IsFlags { get; }

        /// <summary>
        ///  Underlying type of the enum.
        /// </summary>
        public Type UnderlyingType { get; }
    }
}
