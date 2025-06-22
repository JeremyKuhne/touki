namespace System.Diagnostics;

/// <summary>
///  Provides an interpolated string handler for <see cref="Debug.Assert(bool)"/> that only formats when the assert fails.
/// </summary>
[InterpolatedStringHandler]
public ref struct AssertInterpolatedStringHandler
{
    private Touki.ValueStringBuilder _builder;
    private readonly bool _shouldAppend;

    /// <summary>Creates an instance of the handler.</summary>
    /// <param name="literalLength">The length of literal content in the interpolated string.</param>
    /// <param name="formattedCount">The number of interpolation holes in the interpolated string.</param>
    /// <param name="condition">The condition Boolean passed to the consuming method.</param>
    /// <param name="shouldAppend">Indicates whether formatting should proceed.</param>
    public AssertInterpolatedStringHandler(int literalLength, int formattedCount, bool condition, out bool shouldAppend)
    {
        if (condition)
        {
            _builder = default;
            _shouldAppend = shouldAppend = false;
        }
        else
        {
            _builder = new Touki.ValueStringBuilder(literalLength, formattedCount);
            _shouldAppend = shouldAppend = true;
        }
    }

    /// <inheritdoc cref="Touki.ValueStringBuilder.AppendLiteral(string?)"/>
    public void AppendLiteral(string? value)
    {
        if (_shouldAppend)
        {
            _builder.AppendLiteral(value);
        }
    }

    /// <inheritdoc cref="Touki.ValueStringBuilder.AppendFormatted{T}(T)"/>
    public void AppendFormatted<T>(T value)
    {
        if (_shouldAppend)
        {
            _builder.AppendFormatted(value);
        }
    }

    /// <inheritdoc cref="Touki.ValueStringBuilder.AppendFormatted{T}(T,int)"/>
    public void AppendFormatted<T>(T value, int alignment)
    {
        if (_shouldAppend)
        {
            _builder.AppendFormatted(value, alignment);
        }
    }

    /// <inheritdoc cref="Touki.ValueStringBuilder.AppendFormatted{T}(T,int,string?)"/>
    public void AppendFormatted<T>(T value, int alignment, string? format)
    {
        if (_shouldAppend)
        {
            _builder.AppendFormatted<T>(value, alignment, format);
        }
    }

    /// <inheritdoc cref="Touki.ValueStringBuilder.AppendFormatted(ReadOnlySpan{char})"/>
    public void AppendFormatted(scoped ReadOnlySpan<char> value)
    {
        if (_shouldAppend)
        {
            _builder.AppendFormatted(value);
        }
    }

    /// <inheritdoc cref="Touki.ValueStringBuilder.AppendFormatted(ReadOnlySpan{char},int,string?)"/>
    public void AppendFormatted(scoped ReadOnlySpan<char> value, int alignment = 0, string? format = null)
    {
        if (_shouldAppend)
        {
            _builder.AppendFormatted(value, alignment, format);
        }
    }

    /// <inheritdoc cref="Touki.ValueStringBuilder.AppendFormatted(string?)"/>
    public void AppendFormatted(string? value)
    {
        if (_shouldAppend)
        {
            _builder.AppendFormatted(value);
        }
    }

    /// <summary>Gets the built string and clears the handler.</summary>
    public string ToStringAndClear() => _shouldAppend ? _builder.ToStringAndClear() : string.Empty;
}
