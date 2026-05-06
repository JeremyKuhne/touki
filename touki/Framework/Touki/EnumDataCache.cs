// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Concurrent;
using System.Reflection;

namespace Touki;

/// <summary>
///  Per-type cache of enum metadata used by the .NET Framework target.
/// </summary>
/// <remarks>
///  <para>
///   Backed by reflection into <see cref="Enum"/>'s private <c>GetCachedValuesAndNames</c> method;
///   per-type results are computed lazily and cached for the lifetime of the process. Internal
///   because the underlying reflection is fragile against BCL servicing changes.
///  </para>
/// </remarks>
internal static class EnumDataCache
{
    private static readonly ConcurrentDictionary<Type, EnumData> s_enumData = new();

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

    [ThreadStatic]
    private static object[]? t_params;

    /// <summary>
    ///  Gets the values and names for the specified enum <paramref name="type"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Returns the BCL's internal cached arrays - callers must not mutate them.
    ///  </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    ///  <paramref name="type"/> is not an enum type.
    /// </exception>
    public static (ulong[] Values, string[] Names) GetEnumValuesAndNames(Type type)
    {
        if (!type.IsEnum)
        {
            throw new ArgumentException("Type must be an enum.", nameof(type));
        }

        t_params ??= [null!, true];
        object[] parameters = t_params;
        parameters[0] = type;
        object? valuesAndNames = s_cachedNames.Invoke(null, parameters);
        ulong[] values = (ulong[])s_valuesField.GetValue(valuesAndNames)!;
        string[] names = (string[])s_namesField.GetValue(valuesAndNames)!;
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
            Data = GetEnumValuesAndNames(type);
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
