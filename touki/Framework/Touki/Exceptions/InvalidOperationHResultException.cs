// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Windows.Win32.Foundation;

namespace Touki.Exceptions;

internal sealed class InvalidOperationHResultException : Exception
{
    public InvalidOperationHResultException(HRESULT hresult)
        : base($"Invalid operation HRESULT: 0x{((int)hresult):X8}")
    {
        HResult = hresult;
    }
    public InvalidOperationHResultException(int hresult, string message)
        : base(message)
    {
        HResult = hresult;
    }
}
