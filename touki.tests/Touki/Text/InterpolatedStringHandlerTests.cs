// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public unsafe class InterpolatedStringHandlerTests
{
    [Fact]
    public void MinimalInteropolateStringHandler_BasicFunctionality()
    {
        // Use C# interpolated strings with the FomatMinimalHandler method to test the minimal handler.
        string result = FormatMinimalHandler($"Simple");
        result.Should().Be("Simple");

        result = FormatMinimalHandler($"Hello {42}");
        result.Should().Be("Hello 42");
    }

    private static string FormatMinimalHandler(ref MinimalInterpolatedStringHandler handler)
    {
        return handler.ToString();
    }

    /// <summary>
    ///  Bare minimum implementation of an interpolated string handler that can be used with minimal functionality.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct MinimalInterpolatedStringHandler
    {
        private string _value;

        // Always need these two arguments as a miminum
        public MinimalInterpolatedStringHandler(int literalLength, int formattedCount)
        {
            _value = string.Empty;
        }

        // AppendLiteral is mandatory.
        public void AppendLiteral(string value)
        {
            _value += value;
        }

        // Not mandatory, but AppendFormatted overloads are called for all values other than strings.
        public void AppendFormatted<T>(T value)
        {
            _value += value?.ToString();
        }

        public override readonly string ToString() => _value;

        // ToStringAndClear is needed for DefaultInterpolatedStringHandler only.
    }
}
