// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Exceptions;

/// <summary>
///  Helper to allow using new patterns for throwing <see cref="ArgumentNullException"/>s.
/// </summary>
/// <remarks>
///  <para>
///   This can be leveraged in your cross compiled code with global usings. In Touki it is done like this:
///  </para>
///  <para>
///   <code>
///    <![CDATA[#if NETFRAMEWORK
///      global using ArgumentNull = Touki.Exceptions.ArgumentNullAdapter;
///    #else
///      global using ArgumentNull = System.ArgumentNullException;
///    #endif
///    ]]>
///   </code>
///  </para>
/// </remarks>
public static class ArgumentNullAdapter
{
    /// <summary>Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is null.</summary>
    /// <param name="argument">The reference type argument to validate as non-null.</param>
    /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
    public static void ThrowIfNull([NotNull] object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument is null)
        {
            Throw(paramName);
        }
    }

    internal static void ThrowIfNull([NotNull] object? argument, ExceptionArgument paramName)
    {
        if (argument is null)
        {
            ThrowHelper.ThrowArgumentNullException(paramName);
        }
    }

    /// <summary>Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is null.</summary>
    /// <param name="argument">The pointer argument to validate as non-null.</param>
    /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
    public static unsafe void ThrowIfNull([NotNull] void* argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument is null)
        {
            Throw(paramName);
        }
    }

    /// <summary>Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is null.</summary>
    /// <param name="argument">The pointer argument to validate as non-null.</param>
    /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
    internal static void ThrowIfNull(IntPtr argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument == IntPtr.Zero)
        {
            Throw(paramName);
        }
    }

    [DoesNotReturn]
    internal static void Throw(string? paramName) =>
        throw new ArgumentNullException(paramName);
}
