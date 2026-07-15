// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Type-name matching and defaults adapted from dotnet/winforms at
// 73f0222ea7a75610ba883cac9807bd3a003b6d53 (MIT licensed):
// src/System.Private.Windows.Core/src/System/Private/Windows/Nrbf/CoreNrbfSerializer.cs
// src/System.Private.Windows.Core/src/System/Reflection/Metadata/TypeNameComparer.cs

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Runtime.Serialization;
using Touki.Resources.BinaryFormat;

namespace Touki;

/// <summary>
///  Maps type names to types registered through trim-safe generic calls.
/// </summary>
/// <remarks>
///  <para>
///   <see cref="Register{T}"/> roots all members of each registered type for trimming and native AOT. Resolution never
///   loads an assembly or discovers a type dynamically.
///  </para>
///  <para>
///   Types are matched by full name without requiring assembly identities to match. This supports mapping type names
///   written by .NET Framework to their modern .NET equivalents.
///  </para>
///  <para>
///   The dependency-free framework types supported by Windows Forms binary format handling are registered by default.
///  </para>
/// </remarks>
public sealed class RegisteredTypeResolver : ITypeResolver
{
    private readonly Dictionary<TypeName, Type> _types = [with(FullNameTypeNameComparer.Instance)];
    private readonly ConcurrentDictionary<Type, SerializationEvents> _serializationEvents = [];

    /// <summary>
    ///  Creates a resolver containing the default framework type registrations.
    /// </summary>
    public RegisteredTypeResolver()
    {
        RegisterFrameworkTypes();
    }

    /// <summary>
    ///  Registers <typeparamref name="T"/> and roots all of its members for trimming and native AOT.
    /// </summary>
    /// <typeparam name="T">The type to register.</typeparam>
    /// <returns>This resolver.</returns>
    /// <exception cref="InvalidOperationException">
    ///  A different type with the same full name has already been registered.
    /// </exception>
    public RegisteredTypeResolver Register<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
    {
        Type type = typeof(T);
        TypeName typeName = TypeName.Parse(type.AssemblyQualifiedName!);

        if (_types.TryGetValue(typeName, out Type? registeredType) && registeredType != type)
        {
            throw new InvalidOperationException(
                $"Type name '{type.FullName}' is already registered for '{registeredType.AssemblyQualifiedName}'.");
        }

        _types[typeName] = type;
        return this;
    }

    /// <summary>
    ///  Resolves <paramref name="typeName"/> to a registered type.
    /// </summary>
    /// <param name="typeName">The type name to resolve.</param>
    /// <returns>The registered type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is <see langword="null"/>.</exception>
    /// <exception cref="SerializationException">No matching type has been registered.</exception>
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public Type BindToType(TypeName typeName)
    {
        ArgumentNullException.ThrowIfNull(typeName);

        if (_types.TryGetValue(typeName, out Type? type))
        {
            return type;
        }

        throw new SerializationException($"Type '{typeName.AssemblyQualifiedName}' is not registered.");
    }

    bool ITypeResolver.TryBindToType(
        TypeName typeName,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All), NotNullWhen(true)] out Type? type)
    {
        ArgumentNullException.ThrowIfNull(typeName);
        return _types.TryGetValue(typeName, out type);
    }

    [UnconditionalSuppressMessage(
        "ReflectionAnalysis",
        "IL2111:UnrecognizedReflectionPattern",
        Justification = "The type is annotated before it passes through the resolver-bounded cache callback.")]
    internal SerializationEvents GetSerializationEvents(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
        => _serializationEvents.GetOrAdd(type, SerializationEvents.Create);

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "NotSupportedException.TargetSite is not accessed during deserialization.")]
    private void RegisterFrameworkTypes()
    {
        Register<byte>();
        Register<sbyte>();
        Register<short>();
        Register<ushort>();
        Register<int>();
        Register<uint>();
        Register<long>();
        Register<ulong>();
        Register<float>();
        Register<double>();
        Register<char>();
        Register<bool>();
        Register<string>();
        Register<decimal>();
        Register<DateTime>();
        Register<TimeSpan>();
        Register<nint>();
        Register<nuint>();
        Register<NotSupportedException>();

        Register<List<bool>>();
        Register<List<char>>();
        Register<List<string>>();
        Register<List<sbyte>>();
        Register<List<byte>>();
        Register<List<short>>();
        Register<List<ushort>>();
        Register<List<int>>();
        Register<List<uint>>();
        Register<List<long>>();
        Register<List<ulong>>();
        Register<List<float>>();
        Register<List<double>>();
        Register<List<decimal>>();
        Register<List<DateTime>>();
        Register<List<TimeSpan>>();

        Register<byte[]>();
        Register<sbyte[]>();
        Register<short[]>();
        Register<ushort[]>();
        Register<int[]>();
        Register<uint[]>();
        Register<long[]>();
        Register<ulong[]>();
        Register<float[]>();
        Register<double[]>();
        Register<char[]>();
        Register<bool[]>();
        Register<string[]>();
        Register<decimal[]>();
        Register<DateTime[]>();
        Register<TimeSpan[]>();
        Register<object[]>();

        Register<ArrayList>();
        Register<Hashtable>();
    }

    private sealed class FullNameTypeNameComparer : IEqualityComparer<TypeName>
    {
        internal static FullNameTypeNameComparer Instance { get; } = new();

        public bool Equals(TypeName? left, TypeName? right)
        {
            if (left is null || right is null)
            {
                return left is null && right is null;
            }

            if (left.IsArray || right.IsArray)
            {
                return left.IsArray
                    && right.IsArray
                    && left.IsSZArray == right.IsSZArray
                    && left.GetArrayRank() == right.GetArrayRank()
                    && Equals(left.GetElementType(), right.GetElementType());
            }

            if (left.IsConstructedGenericType || right.IsConstructedGenericType)
            {
                if (!left.IsConstructedGenericType
                    || !right.IsConstructedGenericType
                    || !Equals(left.GetGenericTypeDefinition(), right.GetGenericTypeDefinition()))
                {
                    return false;
                }

                ImmutableArray<TypeName> leftArguments = left.GetGenericArguments();
                ImmutableArray<TypeName> rightArguments = right.GetGenericArguments();
                if (leftArguments.Length != rightArguments.Length)
                {
                    return false;
                }

                for (int index = 0; index < leftArguments.Length; index++)
                {
                    if (!Equals(leftArguments[index], rightArguments[index]))
                    {
                        return false;
                    }
                }

                return true;
            }

            return string.Equals(left.FullName, right.FullName, StringComparison.Ordinal);
        }

        public int GetHashCode(TypeName typeName)
        {
            if (typeName.IsArray)
            {
                return HashCode.Combine(
                    typeName.IsSZArray,
                    typeName.GetArrayRank(),
                    GetHashCode(typeName.GetElementType()));
            }

            if (typeName.IsConstructedGenericType)
            {
                HashCode hashCode = new();
                hashCode.Add(GetHashCode(typeName.GetGenericTypeDefinition()));
                foreach (TypeName argument in typeName.GetGenericArguments())
                {
                    hashCode.Add(GetHashCode(argument));
                }

                return hashCode.ToHashCode();
            }

            return StringComparer.Ordinal.GetHashCode(typeName.FullName);
        }
    }
}