// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Text;

/// <summary>
///  Extension methods for <see cref="StringBuilder"/>.
/// </summary>
public static partial class StringBuilderExtensions
{
    extension(StringBuilder builder)
    {
        /// <summary>
        ///  GetChunks returns ChunkEnumerator that follows the IEnumerable pattern and
        ///  thus can be used in a C# 'foreach' statements to retrieve the data in the StringBuilder
        ///  as chunks (ReadOnlyMemory) of characters.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   On .NET Core the returned type is nested in StringBuilder, which we cannot do. As such,
        ///   the full type name has to be different and you must assign this to <see langword="var"/> if you
        ///   need to put it in a local to enable cross compilation.
        ///  </para>
        /// </remarks>
        public ChunkEnumerator GetChunks() => new ChunkEnumerator(builder);
    }
}
