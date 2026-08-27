// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Windows.Win32.Foundation;

namespace Touki.Exceptions;

/// <summary>
///  Represents an invalid-operation failure identified by an HRESULT.
/// </summary>
internal sealed class InvalidOperationHResultException : Exception
{
    /// <inheritdoc cref="InvalidOperationHResultException(int, string)"/>
    /// <param name="hresult">The HRESULT to expose through <see cref="Exception.HResult"/>.</param>
    public InvalidOperationHResultException(HRESULT hresult)
        : base($"Invalid operation HRESULT: 0x{((int)hresult):X8}")
    {
        HResult = hresult;
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="InvalidOperationHResultException"/> class for the specified
    ///  HRESULT.
    /// </summary>
    /// <param name="hresult">The HRESULT to expose through <see cref="Exception.HResult"/>.</param>
    /// <param name="message">The message that describes the error.</param>
    public InvalidOperationHResultException(int hresult, string message)
        : base(message)
    {
        HResult = hresult;
    }
}
