// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Text;

namespace Framework.Touki;

/// <summary>
///  Coverage tests for several small framework polyfills and helpers that
///  were previously at 0% line coverage.
/// </summary>
public class SmallPolyfillCoverageTests
{
    // ---------- ArgumentNullExtensions.ThrowIfNull ----------

    [Fact]
    public void ThrowIfNull_Object_NonNull_DoesNotThrow()
    {
        object value = new();
        Action action = () => ArgumentNullException.ThrowIfNull(value);
        action.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfNull_Object_Null_Throws()
    {
        object? value = null;
        Action action = () => ArgumentNullException.ThrowIfNull(value);
        action.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("value");
    }

    [Fact]
    public unsafe void ThrowIfNull_VoidPointer_NonNull_DoesNotThrow()
    {
        int local = 42;
        void* p = &local;
        Action action = () => ArgumentNullException.ThrowIfNull(p);
        action.Should().NotThrow();
    }

    [Fact]
    public unsafe void ThrowIfNull_VoidPointer_Null_Throws()
    {
        Action action = () => ArgumentNullException.ThrowIfNull((void*)null);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ThrowIfNull_IntPtr_NonZero_DoesNotThrow()
    {
        IntPtr p = new(0x1);
        Action action = () => ArgumentNullException.ThrowIfNull(p);
        action.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfNull_IntPtr_Zero_Throws()
    {
        IntPtr p = IntPtr.Zero;
        Action action = () => ArgumentNullException.ThrowIfNull(p);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ThrowIfNull_Object_ExceptionArgument_NonNull_DoesNotThrow()
    {
        // Internal overload that takes the ExceptionArgument enum.
        object value = new();
        Action action = () => ArgumentNullException.ThrowIfNull(value, ExceptionArgument.value);
        action.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfNull_Object_ExceptionArgument_Null_Throws()
    {
        Action action = () => ArgumentNullException.ThrowIfNull((object?)null, ExceptionArgument.value);
        action.Should().Throw<ArgumentNullException>();
    }

    // ---------- OverflowAdapter ----------

    [Fact]
    public void OverflowAdapter_Throw_ThrowsOverflowException()
    {
        Action action = () => OverflowAdapter.Throw("custom message");
        action.Should().Throw<OverflowException>().WithMessage("custom message");
    }

    [Fact]
    public void OverflowAdapter_Throw_NullMessage_ThrowsOverflowException()
    {
        Action action = () => OverflowAdapter.Throw(null);
        action.Should().Throw<OverflowException>();
    }

    // ---------- ValueStringBuilder.AppendFormatted overloads ----------

    [Fact]
    public void AppendFormatted_Object_NullValue_LeavesEmpty()
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            object? value = null;
            builder.AppendFormatted(value);
            builder.ToString().Should().BeEmpty();
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void AppendFormatted_Object_WithAlignmentAndFormat_FormatsAndPads()
    {
        ValueStringBuilder builder = new(stackalloc char[32]);
        try
        {
            object value = 42;
            builder.AppendFormatted(value, alignment: 6, format: "X4");
            builder.ToString().Should().Be("  002A");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void AppendFormatted_String_WithAlignment_RightAlignsByDefault()
    {
        ValueStringBuilder builder = new(stackalloc char[32]);
        try
        {
            builder.AppendFormatted("hi", alignment: 5, format: null);
            builder.ToString().Should().Be("   hi");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void AppendFormatted_String_WithNegativeAlignment_LeftAligns()
    {
        ValueStringBuilder builder = new(stackalloc char[32]);
        try
        {
            builder.AppendFormatted("hi", alignment: -5, format: null);
            builder.ToString().Should().Be("hi   ");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void AppendFormatted_ReadOnlySpan_LeftAligned_AddsTrailingSpaces()
    {
        ValueStringBuilder builder = new(stackalloc char[32]);
        try
        {
            builder.AppendFormatted("hi".AsSpan(), alignment: -5);
            builder.ToString().Should().Be("hi   ");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void AppendFormatted_GenericValue_WithFormat_UsesFormat()
    {
        ValueStringBuilder builder = new(stackalloc char[32]);
        try
        {
            builder.AppendFormatted<int>(255, "X4");
            builder.ToString().Should().Be("00FF");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void AppendFormatted_GenericValue_WithAlignmentAndStringFormat()
    {
        ValueStringBuilder builder = new(stackalloc char[32]);
        try
        {
            builder.AppendFormatted<int>(7, alignment: 4, format: (string?)null);
            builder.ToString().Should().Be("   7");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void AppendFormatted_ReadOnlySpan_RightAligned_AddsLeadingSpaces()
    {
        ValueStringBuilder builder = new(stackalloc char[32]);
        try
        {
            // Positive alignment + value shorter than alignment hits the right-align
            // (else) branch in AppendFormatted(ROS<char>, alignment, format).
            builder.AppendFormatted("hi".AsSpan(), alignment: 5);
            builder.ToString().Should().Be("   hi");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void AppendFormatted_ReadOnlySpan_AlignmentLessThanValueLength_NoPadding()
    {
        ValueStringBuilder builder = new(stackalloc char[32]);
        try
        {
            builder.AppendFormatted("hello".AsSpan(), alignment: 2);
            builder.ToString().Should().Be("hello");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void AppendFormatted_Value_WithStringFormat()
    {
        ValueStringBuilder builder = new(stackalloc char[32]);
        try
        {
            builder.AppendFormatted(global::Touki.Value.Create(255), "X4");
            builder.ToString().Should().Be("00FF");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void AppendFormatted_GenericT_AsValue_FormatsViaValuePath()
    {
        ValueStringBuilder builder = new(stackalloc char[32]);
        try
        {
            // Hits the typeof(T) == typeof(Value) branch in AppendFormatted<T>.
            global::Touki.Value v = global::Touki.Value.Create(42);
            builder.AppendFormatted(v);
            builder.ToString().Should().Be("42");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void AppendFormat_BraceMismatch_Throws()
    {
        Action action = () =>
        {
            ValueStringBuilder builder = new(stackalloc char[32]);
            try
            {
                global::System.ReadOnlySpan<int> args = stackalloc int[] { 1 };
                // "{}" is malformed: '{' followed by '}' (not an escape, since they don't match
                // for an escape - '{{' or '}}' would). The check is `if (brace != next)` for the
                // escape path; "{}" reaches that with brace='{' next='}' which does NOT match,
                // so FormatError is invoked.
                builder.AppendFormat("{}".AsSpan(), args);
            }
            finally
            {
                builder.Dispose();
            }
        };

        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void AppendFormat_FormatTrailingMissingClose_Throws()
    {
        Action action = () =>
        {
            ValueStringBuilder builder = new(stackalloc char[32]);
            try
            {
                global::System.ReadOnlySpan<int> args = stackalloc int[] { 1 };
                // "{0:X" - format spec without terminating '}' triggers FormatError on the ':' branch.
                builder.AppendFormat("{0:X".AsSpan(), args);
            }
            finally
            {
                builder.Dispose();
            }
        };

        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void AppendFormat_NoClosingBrace_Throws()
    {
        Action action = () =>
        {
            ValueStringBuilder builder = new(stackalloc char[32]);
            try
            {
                global::System.ReadOnlySpan<int> args = stackalloc int[] { 1 };
                // "{0" - hole without terminating brace.
                builder.AppendFormat("{0".AsSpan(), args);
            }
            finally
            {
                builder.Dispose();
            }
        };

        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void AppendFormatted_WithCustomFormatter_UsesFormatter()
    {
        IFormatProvider provider = new CustomFormatProvider();
        ValueStringBuilder builder = new(0, 0, provider, stackalloc char[64]);
        try
        {
            builder.AppendFormatted("hello");
            builder.ToString().Should().Be("[CUSTOM:hello]");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void AppendFormatted_GenericT_WithCustomFormatter_UsesFormatter()
    {
        IFormatProvider provider = new CustomFormatProvider();
        ValueStringBuilder builder = new(0, 0, provider, stackalloc char[64]);
        try
        {
            builder.AppendFormatted<int>(42, format: "X4");
            builder.ToString().Should().Be("[CUSTOM:42]");
        }
        finally
        {
            builder.Dispose();
        }
    }

    private sealed class CustomFormatProvider : IFormatProvider, ICustomFormatter
    {
        public object? GetFormat(Type? formatType) =>
            formatType == typeof(ICustomFormatter) ? this : null;

        public string Format(string? format, object? arg, IFormatProvider? formatProvider) =>
            $"[CUSTOM:{arg}]";
    }

    // ---------- ArgumentOutOfRangeException comparison polyfills ----------

    [Fact]
    public void ThrowIfZero_Int_Zero_Throws()
    {
        Action action = () => ArgumentOutOfRangeException.ThrowIfZero(0);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ThrowIfZero_Int_NonZero_DoesNotThrow()
    {
        Action action = () => ArgumentOutOfRangeException.ThrowIfZero(1);
        action.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfNegative_Int_Negative_Throws()
    {
        Action action = () => ArgumentOutOfRangeException.ThrowIfNegative(-1);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ThrowIfNegative_Int_NonNegative_DoesNotThrow()
    {
        Action action = () =>
        {
            ArgumentOutOfRangeException.ThrowIfNegative(0);
            ArgumentOutOfRangeException.ThrowIfNegative(1);
        };
        action.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfNegativeOrZero_Int_Zero_Throws()
    {
        Action action = () => ArgumentOutOfRangeException.ThrowIfNegativeOrZero(0);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ThrowIfEqual_Equal_Throws()
    {
        Action action = () => ArgumentOutOfRangeException.ThrowIfEqual(5, 5);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ThrowIfEqual_Different_DoesNotThrow()
    {
        Action action = () => ArgumentOutOfRangeException.ThrowIfEqual(5, 7);
        action.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfNotEqual_Different_Throws()
    {
        Action action = () => ArgumentOutOfRangeException.ThrowIfNotEqual(5, 7);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ThrowIfNotEqual_Equal_DoesNotThrow()
    {
        Action action = () => ArgumentOutOfRangeException.ThrowIfNotEqual(5, 5);
        action.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfGreaterThan_Greater_Throws()
    {
        Action action = () => ArgumentOutOfRangeException.ThrowIfGreaterThan(10, 5);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ThrowIfGreaterThan_LessOrEqual_DoesNotThrow()
    {
        Action action = () =>
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(5, 5);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(3, 5);
        };
        action.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfGreaterThanOrEqual_GreaterOrEqual_Throws()
    {
        Action a1 = () => ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(10, 5);
        Action a2 = () => ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(5, 5);
        a1.Should().Throw<ArgumentOutOfRangeException>();
        a2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ThrowIfLessThan_Less_Throws()
    {
        Action action = () => ArgumentOutOfRangeException.ThrowIfLessThan(3, 5);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ThrowIfLessThan_GreaterOrEqual_DoesNotThrow()
    {
        Action action = () =>
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(5, 5);
            ArgumentOutOfRangeException.ThrowIfLessThan(7, 5);
        };
        action.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfLessThanOrEqual_LessOrEqual_Throws()
    {
        Action a1 = () => ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(3, 5);
        Action a2 = () => ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(5, 5);
        a1.Should().Throw<ArgumentOutOfRangeException>();
        a2.Should().Throw<ArgumentOutOfRangeException>();
    }

    // Non-throw paths for every overload; the existing test suite only validates
    // the throwing branches, leaving the closing braces of each method uncovered.

    [Fact]
    public void ThrowIfZero_AllOverloads_NonZero_DoesNotThrow()
    {
        Action action = () =>
        {
            ArgumentOutOfRangeException.ThrowIfZero(1L);
            ArgumentOutOfRangeException.ThrowIfZero(1u);
            ArgumentOutOfRangeException.ThrowIfZero(1ul);
            ArgumentOutOfRangeException.ThrowIfZero((nint)1);
            ArgumentOutOfRangeException.ThrowIfZero((nuint)1);
            ArgumentOutOfRangeException.ThrowIfZero(1f);
            ArgumentOutOfRangeException.ThrowIfZero(1d);
            ArgumentOutOfRangeException.ThrowIfZero(1m);
        };

        action.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfNegative_AllOverloads_NonNegative_DoesNotThrow()
    {
        Action action = () =>
        {
            ArgumentOutOfRangeException.ThrowIfNegative(0L);
            ArgumentOutOfRangeException.ThrowIfNegative((nint)0);
            ArgumentOutOfRangeException.ThrowIfNegative(0f);
            ArgumentOutOfRangeException.ThrowIfNegative(0d);
            ArgumentOutOfRangeException.ThrowIfNegative(0m);
        };

        action.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfNegativeOrZero_AllOverloads_Positive_DoesNotThrow()
    {
        Action action = () =>
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(1L);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero((nint)1);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(1f);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(1d);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(1m);
        };

        action.Should().NotThrow();
    }

    // ---------- EnumerationMatcherExtensions ----------

    [Fact]
    public void EnumerationMatcherExtensions_MatchesDirectory_ForwardsToInterface()
    {
        TestMatcher matcher = new() { DirectoryResult = true };
        // Call the extension method explicitly: instance methods (including interface
        // methods that accept ReadOnlySpan<char> which absorbs string via implicit
        // conversion) win during overload resolution otherwise.
        bool actual = EnumerationMatcherExtensions.MatchesDirectory(matcher, "C:\\root", "sub", matchForExclusion: true);
        actual.Should().BeTrue();
        matcher.LastMatchForExclusion.Should().BeTrue();
    }

    [Fact]
    public void EnumerationMatcherExtensions_MatchesDirectory_DefaultsToInclusion()
    {
        TestMatcher matcher = new() { DirectoryResult = false };
        bool actual = EnumerationMatcherExtensions.MatchesDirectory(matcher, "C:\\root", "sub");
        actual.Should().BeFalse();
        matcher.LastMatchForExclusion.Should().BeFalse();
    }

    [Fact]
    public void EnumerationMatcherExtensions_MatchesFile_ForwardsToInterface()
    {
        TestMatcher matcher = new() { FileResult = true };
        bool actual = EnumerationMatcherExtensions.MatchesFile(matcher, "C:\\root", "file.txt");
        actual.Should().BeTrue();
    }

    private sealed class TestMatcher : IEnumerationMatcher
    {
        public bool DirectoryResult { get; set; }
        public bool FileResult { get; set; }
        public bool LastMatchForExclusion { get; private set; }

        public void DirectoryFinished()
        {
        }

        public void Dispose()
        {
        }

        public bool MatchesDirectory(ReadOnlySpan<char> currentDirectory, ReadOnlySpan<char> directoryName, bool matchForExclusion)
        {
            LastMatchForExclusion = matchForExclusion;
            return DirectoryResult;
        }

        public bool MatchesFile(ReadOnlySpan<char> currentDirectory, ReadOnlySpan<char> fileName) => FileResult;
    }

    // ---------- EnumDataCache.EnumData ----------

    [Flags]
    private enum SampleFlags
    {
        A = 1,
        B = 2
    }

    [Fact]
    public void EnumData_Constructor_NonEnum_Throws()
    {
        Action action = () => new global::Touki.EnumDataCache.EnumData(typeof(int));
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EnumData_Constructor_Enum_PopulatesType()
    {
        global::Touki.EnumDataCache.EnumData data = new(typeof(SampleFlags));
        data.Type.Should().Be(typeof(SampleFlags));
        data.IsFlags.Should().BeTrue();
        data.UnderlyingType.Should().Be(typeof(int));
        data.Data.Names.Should().Contain("A");
    }

    // ---------- ValueStringBuilder.FormatterHelper<T> ----------

    private struct NonFormattableStruct
    {
        public int Value;
    }

    [Fact]
    public void AppendFormatted_NonISpanFormattableStruct_FallsBackToObjectFormat()
    {
        // Exercises FormatterHelper<T>.Init's branch where T does not implement
        // ISpanFormattable (the helper returns null and the builder falls back
        // to boxed formatting).
        ValueStringBuilder builder = new(stackalloc char[32]);
        try
        {
            NonFormattableStruct value = new() { Value = 7 };
            builder.AppendFormatted(value);
            // The fall-back path boxes and calls ToString().
            builder.AsSpan().ToString().Should().Be(value.ToString());
        }
        finally
        {
            builder.Dispose();
        }
    }
}
