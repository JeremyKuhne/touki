// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Windows.Win32.Foundation;

namespace Touki.Exceptions;

[TestClass]
public class HResultExceptionTests
{
    [TestMethod]
    public void HResultException_HResultCtor_FormatsHexMessage()
    {
        HRESULT hresult = (HRESULT)unchecked((int)0x80004005);
        HResultException exception = new(hresult);
        exception.HResult.Should().Be(unchecked((int)0x80004005));
        exception.Message.Should().Be("HRESULT: 0x80004005");
    }

    [TestMethod]
    public void HResultException_IntMessageCtor_PreservesBoth()
    {
        HResultException exception = new(unchecked((int)0x80070005), "Access denied");
        exception.HResult.Should().Be(unchecked((int)0x80070005));
        exception.Message.Should().Be("Access denied");
    }

    [TestMethod]
    public void HResultException_ZeroSuccessHResult_PreservesValue()
    {
        HRESULT hresult = (HRESULT)0;
        HResultException exception = new(hresult);
        exception.HResult.Should().Be(0);
        exception.Message.Should().Be("HRESULT: 0x00000000");
    }

    [TestMethod]
    public void InvalidOperationHResultException_HResultCtor_FormatsHexMessage()
    {
        HRESULT hresult = (HRESULT)unchecked((int)0x80004001);
        InvalidOperationHResultException exception = new(hresult);
        exception.HResult.Should().Be(unchecked((int)0x80004001));
        exception.Message.Should().Be("Invalid operation HRESULT: 0x80004001");
    }

    [TestMethod]
    public void InvalidOperationHResultException_IntMessageCtor_PreservesBoth()
    {
        InvalidOperationHResultException exception = new(unchecked((int)0x8000FFFF), "Catastrophic failure");
        exception.HResult.Should().Be(unchecked((int)0x8000FFFF));
        exception.Message.Should().Be("Catastrophic failure");
    }
}
