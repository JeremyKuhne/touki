// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Providers;

public class NullClipboardProviderTests
{
    [Fact]
    public void HasText_Always_ReturnsFalse()
    {
        NullClipboardProvider.Instance.HasText.Should().BeFalse();
    }

    [Fact]
    public void TryGetText_Always_ReturnsFalseWithNullOut()
    {
        NullClipboardProvider.Instance.TryGetText(out string? text).Should().BeFalse();
        text.Should().BeNull();
    }

    [Fact]
    public void TrySetText_Always_ReturnsFalse()
    {
        NullClipboardProvider.Instance.TrySetText("anything".AsSpan()).Should().BeFalse();
    }

    [Fact]
    public void TryClear_Always_ReturnsFalse()
    {
        NullClipboardProvider.Instance.TryClear().Should().BeFalse();
    }

    [Fact]
    public void IsAvailable_Always_ReturnsFalse()
    {
        NullClipboardProvider.Instance.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void Instance_IsSingleton()
    {
        NullClipboardProvider first = NullClipboardProvider.Instance;
        NullClipboardProvider second = NullClipboardProvider.Instance;
        first.Should().BeSameAs(second);
    }
}
