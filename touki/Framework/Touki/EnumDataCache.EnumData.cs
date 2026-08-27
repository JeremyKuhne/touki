// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

internal static partial class EnumDataCache
{
    /// <summary>
    ///  Cached data for an enum type, including its values, names, and whether it is a flags enum.
    /// </summary>
    public class EnumData
    {
        /// <summary>
        ///  Initializes a new instance of the <see cref="EnumData"/> class.
        /// </summary>
        /// <param name="type">The enum type to describe.</param>
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
