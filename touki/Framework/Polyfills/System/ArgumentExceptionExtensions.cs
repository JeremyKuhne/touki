// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Framework.Resources;

namespace System;

/// <summary>
///  Helper to allow using new patterns for throwing <see cref="ArgumentException"/>s.
/// </summary>
public static class ArgumentExceptionExtensions
{
    extension(ArgumentException)
    {
        /// <summary>
        ///  Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is <see langword="null"/>,
        ///  or an <see cref="ArgumentException"/> if it is empty.
        /// </summary>
        /// <param name="argument">The string argument to validate as non-null and non-empty.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
        public static void ThrowIfNullOrEmpty(
            [NotNull] string? argument,
            [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (argument is null)
            {
                throw new ArgumentNullException(paramName);
            }

            if (argument.Length == 0)
            {
                throw new ArgumentException(SRF.Argument_EmptyString, paramName);
            }
        }

        /// <summary>
        ///  Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is <see langword="null"/>,
        ///  or an <see cref="ArgumentException"/> if it is empty or consists only of white-space characters.
        /// </summary>
        /// <param name="argument">The string argument to validate.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
        public static void ThrowIfNullOrWhiteSpace(
            [NotNull] string? argument,
            [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (argument is null)
            {
                throw new ArgumentNullException(paramName);
            }

            if (string.IsNullOrWhiteSpace(argument))
            {
                throw new ArgumentException(SRF.Argument_EmptyOrWhiteSpaceString, paramName);
            }
        }
    }
}
