// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Extension methods for <see cref="TextWriter"/>.
/// </summary>
public static partial class TextWriterExtensions
{
    extension(TextWriter writer)
    {
        /// <summary>
        ///  Allows writing a <see cref="StringSegment"/> to a <see cref="TextWriter"/>.
        /// </summary>
        public void Write(StringSegment value) => value.WriteTo(writer);

        /// <summary>
        ///  Allows writing a <see cref="StringSegment"/> to a <see cref="TextWriter"/>.
        /// </summary>
        public void WriteLine(StringSegment value)
        {
            value.WriteTo(writer);
            writer.WriteLine();
        }

        /// <summary>
        ///  Writes an interpolated string directly to a <see cref="StreamWriter"/>.
        /// </summary>
        public void WriteFormatted(ref ValueStringBuilder builder)
        {
            if (builder.Length > 0)
            {
                builder.CopyTo(writer);
                builder.Clear();
            }
        }

#if NET
        /// <summary>
        ///  Writes a string directly to a <see cref="TextWriter"/>.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   Optimization overload that allows string literals to be used without creating a builder.
        ///  </para>
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void WriteFormatted(string value)
        {
            // While it would be nice to have this for .NET Framework, the only method we have on
            // StreamWriter takes a char[] buffer. We can't reinterpret the string as a char[].
            writer.Write(value.AsSpan());
        }
#endif
    }
}
