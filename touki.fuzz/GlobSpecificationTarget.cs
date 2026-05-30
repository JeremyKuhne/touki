// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;
using Touki.Text;

namespace Touki.Fuzz;

/// <summary>
///  Coverage-guided fuzz target for <see cref="GlobSpecification"/>, the compiled glob
///  specification produced by the <c>Compile</c> / <c>TryCompile</c> family.
/// </summary>
/// <remarks>
///  <para>
///   The fuzz input is decoded as an opcode stream: a small header selects the
///   <see cref="GlobDialect"/>, <see cref="GlobOptions"/>, <see cref="GlobPathSeparator"/>,
///   and <c>maxPatternLength</c>; the next chunk becomes the pattern; the remainder
///   becomes a handful of match inputs. Pattern and input characters are drawn from a
///   curated glob alphabet (wildcards, classes, braces, extglob punctuation, escapes,
///   both separators, dots, and a few literals) so the fuzzer reaches the grammar
///   quickly instead of wandering through arbitrary code points.
///  </para>
///  <para>
///   Every public method that accepts a pattern is exercised: both
///   <see cref="GlobSpecification.TryCompile(StringSegment, GlobDialect, GlobOptions, GlobPathSeparator, int, out GlobSpecification, out GlobCompileError)"/>
///   overloads, <see cref="GlobSpecification.Compile(StringSegment, GlobDialect, GlobOptions, GlobPathSeparator, int)"/>,
///   <see cref="GlobSpecification.IsMatch(ReadOnlySpan{char})"/>, and
///   <see cref="GlobSpecification.CreateMatcher(string)"/>. The oracles are differential:
///  </para>
///  <para>
///   <list type="bullet">
///    <item><description><c>Compile</c> throws <see cref="GlobFormatException"/> if and only if the
///     7-argument <c>TryCompile</c> reports failure.</description></item>
///    <item><description>The 5-argument <c>TryCompile</c> agrees with the 7-argument overload when the
///     latter is given <see cref="GlobPathSeparator.DialectDefault"/> and an unbounded length.</description></item>
///    <item><description>Compilation is deterministic: a second compile of the same inputs yields the same
///     flags and the same <c>IsMatch</c> verdicts.</description></item>
///    <item><description>A pattern longer than a non-negative <c>maxPatternLength</c> always fails to compile,
///     and a <see cref="GlobCompileErrorCode.PatternTooLarge"/> failure only happens when it is oversized.</description></item>
///    <item><description><see cref="GlobSpecification.Pattern"/> round-trips the supplied pattern verbatim.</description></item>
///    <item><description><see cref="GlobSpecification.IsMatch(ReadOnlySpan{char})"/> is pure: repeated calls return the same verdict.</description></item>
///   </list>
///  </para>
/// </remarks>
internal static class GlobSpecificationTarget
{
    // Bound pattern / input lengths so a fuzzer-grown input cannot allocate or backtrack unbounded.
    private const int MaxPattern = 128;
    private const int MaxInput = 128;

    // Win32 is intentionally excluded: it is reserved-but-unimplemented (compiling rejects it) and
    // referencing the [Obsolete] member would break the warnings-as-errors build.
    private static readonly GlobDialect[] s_dialects =
    [
        GlobDialect.Posix,
        GlobDialect.PosixPath,
        GlobDialect.Bash,
        GlobDialect.Git,
        GlobDialect.MSBuild,
        GlobDialect.FileSystemGlobbing,
        GlobDialect.Simple,
        GlobDialect.PowerShell
    ];

    // Dense glob alphabet: every metacharacter the supported dialects care about plus both
    // separators, dots, escapes, and a few ordinary literals.
    private static readonly char[] s_alphabet =
    [
        '*', '?', '[', ']', '{', '}', '(', ')', '|', '!',
        '\\', '/', '.', '-', '^', '+', '@', '`', ':', ',',
        'a', 'b', 'c', 'A', 'B', '0', '1', ' ', '~', 'z'
    ];

    internal static void Run(ReadOnlySpan<byte> data)
    {
        SpanReader<byte> reader = new(data);

        GlobDialect dialect = s_dialects[NextByte(ref reader) % s_dialects.Length];
        GlobOptions options = (GlobOptions)(NextByte(ref reader) & 0x1F);
        GlobPathSeparator separator = (GlobPathSeparator)(NextByte(ref reader) % 4);

        string patternString = ReadGlobString(ref reader, MaxPattern);
        StringSegment pattern = patternString;

        // Straddle the maxPatternLength boundary: even -> disabled, odd -> a value in [0, length + 1].
        byte maxByte = NextByte(ref reader);
        int maxPatternLength = (maxByte & 1) == 0 ? -1 : maxByte % (patternString.Length + 2);

        int inputCount = NextByte(ref reader) % 4;
        string[] inputs = new string[inputCount + 1];
        inputs[0] = string.Empty;
        for (int i = 0; i < inputCount; i++)
        {
            inputs[i + 1] = ReadGlobString(ref reader, MaxInput);
        }

        Drive(pattern, patternString, dialect, options, separator, maxPatternLength, inputs);
    }

    private static void Drive(
        StringSegment pattern,
        string patternString,
        GlobDialect dialect,
        GlobOptions options,
        GlobPathSeparator separator,
        int maxPatternLength,
        string[] inputs)
    {
        bool oversize = maxPatternLength >= 0 && patternString.Length > maxPatternLength;

        bool tryOk = GlobSpecification.TryCompile(
            pattern,
            dialect,
            options,
            separator,
            maxPatternLength,
            out GlobSpecification? spec,
            out GlobCompileError error);

        // Oversized patterns must never compile.
        if (oversize && tryOk)
        {
            throw new FuzzInvariantException("Pattern longer than maxPatternLength compiled successfully.");
        }

        if (!tryOk && spec is not null)
        {
            throw new FuzzInvariantException("TryCompile returned false but produced a specification.");
        }

        if (!tryOk && error.Code == GlobCompileErrorCode.None)
        {
            throw new FuzzInvariantException("TryCompile failed without an error code.");
        }

        // Compile must agree with TryCompile on success / failure.
        GlobFormatException? thrown = null;
        GlobSpecification? compiled = null;
        try
        {
            compiled = GlobSpecification.Compile(pattern, dialect, options, separator, maxPatternLength);
        }
        catch (GlobFormatException ex)
        {
            thrown = ex;
        }

        try
        {
            if (tryOk != (thrown is null))
            {
                throw new FuzzInvariantException("Compile and TryCompile disagree on whether the pattern is valid.");
            }

            if (!tryOk)
            {
                // Nothing further to validate on the failure path.
                return;
            }

            // spec is non-null on success (NotNullWhen(true)); compiled mirrors it.
            CheckSpecification(spec!, pattern, patternString, dialect, inputs);

            if (compiled is not null)
            {
                AssertSameMatches(spec!, compiled, inputs, "Compile vs TryCompile");
            }

            // Determinism: a second compile with identical inputs must behave identically.
            bool tryOk2 = GlobSpecification.TryCompile(
                pattern,
                dialect,
                options,
                separator,
                maxPatternLength,
                out GlobSpecification? spec2,
                out _);

            if (!tryOk2 || spec2 is null)
            {
                throw new FuzzInvariantException("Re-compiling a valid pattern failed.");
            }

            try
            {
                AssertSameMatches(spec!, spec2, inputs, "Compile determinism");
            }
            finally
            {
                spec2.Dispose();
            }

            // The 5-argument TryCompile must equal the 7-argument overload given the same defaults.
            CheckShortOverloadEquivalence(pattern, dialect, options, inputs);
        }
        finally
        {
            spec?.Dispose();
            compiled?.Dispose();
        }
    }

    private static void CheckSpecification(
        GlobSpecification spec,
        StringSegment pattern,
        string patternString,
        GlobDialect dialect,
        string[] inputs)
    {
        // Flag accessors must not throw and the dialect must round-trip.
        if (spec.Dialect != dialect)
        {
            throw new FuzzInvariantException("Compiled specification reports a different dialect than requested.");
        }

        _ = spec.Options;
        _ = spec.Separator;
        _ = spec.Negated;
        _ = spec.RootAnchored;
        _ = spec.DirectoryOnly;
        _ = spec.LiteralPathPrefix;

        // Pattern round-trips the user-supplied input verbatim.
        if (!spec.Pattern.AsSpan().SequenceEqual(pattern.AsSpan()))
        {
            throw new FuzzInvariantException("Compiled specification did not round-trip the supplied pattern.");
        }

        foreach (string input in inputs)
        {
            bool first = spec.IsMatch(input.AsSpan());
            bool second = spec.IsMatch(input.AsSpan());
            if (first != second)
            {
                throw new FuzzInvariantException("IsMatch is not pure: repeated calls disagree.");
            }
        }

        // CreateMatcher must not throw for a null or a synthesized root.
        using GlobMatch matcherNull = spec.CreateMatcher(null);
        using GlobMatch matcherRoot = spec.CreateMatcher(patternString.Length == 0 ? "." : patternString);
    }

    private static void CheckShortOverloadEquivalence(
        StringSegment pattern,
        GlobDialect dialect,
        GlobOptions options,
        string[] inputs)
    {
        bool try5 = GlobSpecification.TryCompile(
            pattern,
            dialect,
            options,
            out GlobSpecification? spec5,
            out _);

        bool try7 = GlobSpecification.TryCompile(
            pattern,
            dialect,
            options,
            GlobPathSeparator.DialectDefault,
            maxPatternLength: -1,
            out GlobSpecification? spec7,
            out _);

        try
        {
            if (try5 != try7)
            {
                throw new FuzzInvariantException("5-argument and 7-argument TryCompile disagree on success.");
            }

            if (try5 && spec5 is not null && spec7 is not null)
            {
                AssertSameMatches(spec5, spec7, inputs, "TryCompile overload equivalence");
            }
        }
        finally
        {
            spec5?.Dispose();
            spec7?.Dispose();
        }
    }

    private static void AssertSameMatches(GlobSpecification a, GlobSpecification b, string[] inputs, string context)
    {
        foreach (string input in inputs)
        {
            if (a.IsMatch(input.AsSpan()) != b.IsMatch(input.AsSpan()))
            {
                throw new FuzzInvariantException($"{context}: IsMatch verdicts diverge.");
            }
        }
    }

    private static string ReadGlobString(ref SpanReader<byte> reader, int maxLength)
    {
        int length = NextByte(ref reader) % (maxLength + 1);
        if (length == 0)
        {
            return string.Empty;
        }

        char[] chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = s_alphabet[NextByte(ref reader) % s_alphabet.Length];
        }

        return new string(chars);
    }

    private static byte NextByte(ref SpanReader<byte> reader) => reader.TryRead(out byte value) ? value : (byte)0;
}
