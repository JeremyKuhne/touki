namespace Touki;

/// <summary>
///  Helper methods that mirror <see cref="Debug"/>.
/// </summary>
public static class Debugging
{
    /// <inheritdoc cref="Debug.Assert(bool)"/>
    [Conditional("DEBUG")]
    public static void Assert([DoesNotReturnIf(false)] bool condition) => Debug.Assert(condition);

    /// <inheritdoc cref="Debug.Assert(bool,string?)"/>
    [Conditional("DEBUG")]
    public static void Assert([DoesNotReturnIf(false)] bool condition, string? message) => Debug.Assert(condition, message);

#if NET9_0_OR_GREATER
    /// <inheritdoc cref="Debug.Assert(bool,ref Debug.AssertInterpolatedStringHandler)"/>
    [Conditional("DEBUG")]
    public static void Assert([DoesNotReturnIf(false)] bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] ref Debug.AssertInterpolatedStringHandler message)
        => Debug.Assert(condition, ref message);
#else
    /// <inheritdoc cref="Debug.Assert(bool,string?)"/>
    [Conditional("DEBUG")]
    public static void Assert([DoesNotReturnIf(false)] bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] ref System.Diagnostics.AssertInterpolatedStringHandler message)
    {
        if (!condition)
        {
            Debug.Fail(message.ToStringAndClear());
        }
    }
#endif

    /// <inheritdoc cref="Debug.Assert(bool,string?,string?)"/>
    [Conditional("DEBUG")]
    public static void Assert([DoesNotReturnIf(false)] bool condition, string? message, string? detailMessage)
        => Debug.Assert(condition, message, detailMessage);

#if NET9_0_OR_GREATER
    /// <inheritdoc cref="Debug.Assert(bool,ref Debug.AssertInterpolatedStringHandler,ref Debug.AssertInterpolatedStringHandler)"/>
    [Conditional("DEBUG")]
    public static void Assert([DoesNotReturnIf(false)] bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] ref Debug.AssertInterpolatedStringHandler message,
        [InterpolatedStringHandlerArgument(nameof(condition))] ref Debug.AssertInterpolatedStringHandler detailMessage)
        => Debug.Assert(condition, ref message, ref detailMessage);
#else
    /// <inheritdoc cref="Debug.Assert(bool,string?,string?)"/>
    [Conditional("DEBUG")]
    public static void Assert([DoesNotReturnIf(false)] bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] ref System.Diagnostics.AssertInterpolatedStringHandler message,
        [InterpolatedStringHandlerArgument(nameof(condition))] ref System.Diagnostics.AssertInterpolatedStringHandler detailMessage)
    {
        if (!condition)
        {
            Debug.Fail(message.ToStringAndClear(), detailMessage.ToStringAndClear());
        }
    }
#endif

    /// <inheritdoc cref="Debug.Assert(bool,string?,string,System.Object[])"/>
    [Conditional("DEBUG")]
    public static void Assert([DoesNotReturnIf(false)] bool condition, string? message,
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string detailMessageFormat, params object?[] args)
        => Debug.Assert(condition, message, detailMessageFormat, args);

    /// <inheritdoc cref="Debug.Fail(string?)"/>
    [Conditional("DEBUG"), DoesNotReturn]
    public static void Fail(string? message) => Debug.Fail(message);

    /// <inheritdoc cref="Debug.Fail(string?,string?)"/>
    [Conditional("DEBUG"), DoesNotReturn]
    public static void Fail(string? message, string? detailMessage) => Debug.Fail(message, detailMessage);
}
