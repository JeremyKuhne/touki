// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Windows.Win32.Foundation;

namespace Touki.Exceptions;

/// <summary>
///  Wraps an HRESULT as an exception while preserving its numeric error code.
/// </summary>
internal sealed class HResultException : Exception
{
    /// <inheritdoc cref="HResultException(int, string)"/>
    /// <param name="hresult">The HRESULT to expose through <see cref="Exception.HResult"/>.</param>
    public HResultException(HRESULT hresult)
        : base($"HRESULT: 0x{((int)hresult):X8}")
    {
        HResult = hresult;
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="HResultException"/> class for the specified HRESULT.
    /// </summary>
    /// <param name="hresult">The HRESULT to expose through <see cref="Exception.HResult"/>.</param>
    /// <param name="message">The message that describes the error.</param>
    public HResultException(int hresult, string message)
        : base(message)
    {
        HResult = hresult;
    }
}
