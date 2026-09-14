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
internal static partial class EnumDataCache
{
    private static readonly ConcurrentDictionary<Type, EnumData> s_enumData = new();

    private static readonly MethodInfo s_cachedNames;
    private static readonly FieldInfo s_valuesField;
    private static readonly FieldInfo s_namesField;

    [ThreadStatic]
    private static object?[]? t_params;

    static EnumDataCache()
    {
        s_cachedNames = typeof(Enum).GetMethod(
            "GetCachedValuesAndNames",
            BindingFlags.NonPublic | BindingFlags.Static) ?? throw new InvalidOperationException();

        Type valuesAndNames = typeof(Enum).GetNestedType(
            "ValuesAndNames",
            BindingFlags.NonPublic) ?? throw new InvalidOperationException();

        s_valuesField = valuesAndNames.GetField(
            "Values",
            BindingFlags.Public | BindingFlags.Instance) ?? throw new InvalidOperationException();

        s_namesField = valuesAndNames.GetField(
            "Names",
            BindingFlags.Public | BindingFlags.Instance) ?? throw new InvalidOperationException();
    }

    /// <summary>
    ///  Gets the values and names for the specified enum <paramref name="type"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Returns the BCL's internal cached arrays - callers must not mutate them.
    ///  </para>
    /// </remarks>
    /// <param name="type">The enum type whose values and names to retrieve.</param>
    /// <returns>The cached values and corresponding names for <paramref name="type"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="type"/> is not an enum type.</exception>
    public static (ulong[] Values, string[] Names) GetEnumValuesAndNames(Type type)
    {
        if (!type.IsEnum)
        {
            throw new ArgumentException("Type must be an enum.", nameof(type));
        }

        t_params ??= [null, true];
        object?[] parameters = t_params;
        parameters[0] = type;
        object valuesAndNames = s_cachedNames.Invoke(obj: null, parameters) ?? throw new InvalidOperationException();
        ulong[] values = s_valuesField.GetValue(valuesAndNames) as ulong[] ?? throw new InvalidOperationException();
        string[] names = s_namesField.GetValue(valuesAndNames) as string[] ?? throw new InvalidOperationException();
        return (values, names);
    }

    /// <summary>
    ///  Gets cached data for an enum type, including its values, names, and whether it is a flags enum.
    /// </summary>
    /// <param name="type">The enum type whose metadata to retrieve.</param>
    /// <returns>The cached metadata for <paramref name="type"/>.</returns>
    public static EnumData GetEnumData(Type type) => s_enumData.GetOrAdd(type, t => new EnumData(t));
}
