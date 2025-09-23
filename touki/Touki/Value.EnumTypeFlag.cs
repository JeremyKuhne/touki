// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public readonly partial struct Value
{
    private sealed class EnumTypeFlag<T> : TypeFlag<T>, IEnumType
    {
        public static EnumTypeFlag<T> Instance { get; } = new();
        public override T To(in Value value) => Unsafe.As<Union, T>(ref Unsafe.AsRef(in value._union));

        public static Type UnderlyingType { get; } = typeof(T).GetEnumUnderlyingType();

        Type IEnumType.UnderlyingType => UnderlyingType;

        public static bool IsSigned { get; } = UnderlyingType == typeof(sbyte)
            || UnderlyingType == typeof(short)
            || UnderlyingType == typeof(int)
            || UnderlyingType == typeof(long);

        bool IEnumType.IsSigned => IsSigned;

        ulong IEnumType.AsUlong(in Value value)
        {
            if (!IsSigned)
            {
                return value._union.UInt64;
            }

            return Size switch
            {
                1 => (ulong)(long)value._union.SByte,
                2 => (ulong)(long)value._union.Int16,
                4 => (ulong)(long)value._union.Int32,
                8 => (ulong)value._union.Int64,
                _ => throw new InvalidOperationException($"Unsupported enum size: {Size}.")
            };
        }

        public static int Size { get; } = Unsafe.SizeOf<T>();

        int IEnumType.Size => Size;

#if DEBUG
        public static bool Validated { get; } = typeof(T).IsEnum
            ? true
            : throw new InvalidOperationException($"Type '{typeof(T).FullName}' must be an enum type to use {nameof(EnumTypeFlag<T>)}.");
#endif
    }

    private interface IEnumType
    {
        Type UnderlyingType { get; }
        bool IsSigned { get; }
        int Size { get; }
        ulong AsUlong(in Value value);
    }
}
