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
        ///  Writes an interpolated string to a <see cref="TextWriter"/>.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   This method consumes and disposes <paramref name="builder"/>.
        ///  </para>
        ///  <para>
        ///   Exact <see cref="StringWriter"/> and <see cref="StreamWriter"/> instances use an optimized direct
        ///   copy. Custom writer types are passed a string through <see cref="TextWriter.Write(string)"/> so their
        ///   virtual behavior is preserved.
        ///  </para>
        /// </remarks>
        public void WriteFormatted(ref ValueStringBuilder builder)
        {
            try
            {
                Type writerType = writer.GetType();
                if (writerType == typeof(StringWriter))
                {
                    // Preserve the state check and empty-write behavior of the original virtual string call.
                    writer.Write(string.Empty);
                    builder.CopyTo(writer);
                }
                else if (writerType == typeof(StreamWriter))
                {
                    if (builder.Length == 0)
                    {
                        writer.Write(string.Empty);
                    }
                    else
                    {
                        builder.CopyTo(writer);
                    }
                }
                else
                {
                    // Preserve virtual Write(string) behavior for custom TextWriter implementations.
                    writer.Write(builder.ToString());
                }
            }
            finally
            {
                builder.Dispose();
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
            Type writerType = writer.GetType();
            if (writerType == typeof(StringWriter) || writerType == typeof(StreamWriter))
            {
                writer.Write(value.AsSpan());
            }
            else
            {
                writer.Write(value);
            }
        }
#endif
    }
}
