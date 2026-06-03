// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Providers;

[TestClass]
public class NullClipboardProviderTests
{
    [TestMethod]
    public void HasText_Always_ReturnsFalse()
    {
        NullClipboardProvider.Instance.HasText.Should().BeFalse();
    }

    [TestMethod]
    public void TryGetText_Always_ReturnsFalseWithNullOut()
    {
        NullClipboardProvider.Instance.TryGetText(out string? text).Should().BeFalse();
        text.Should().BeNull();
    }

    [TestMethod]
    public void TrySetText_Always_ReturnsFalse()
    {
        NullClipboardProvider.Instance.TrySetText("anything".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public void TryClear_Always_ReturnsFalse()
    {
        NullClipboardProvider.Instance.TryClear().Should().BeFalse();
    }

    [TestMethod]
    public void IsAvailable_Always_ReturnsFalse()
    {
        NullClipboardProvider.Instance.IsAvailable.Should().BeFalse();
    }

    [TestMethod]
    public void Instance_IsSingleton()
    {
        NullClipboardProvider first = NullClipboardProvider.Instance;
        NullClipboardProvider second = NullClipboardProvider.Instance;
        first.Should().BeSameAs(second);
    }
}
