// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Interop;

public class HandleRefTests
{
    private sealed class TestWrapper
    {
    }

    [Fact]
    public void Ctor_WrapperAndHandle_AssignsBoth()
    {
        TestWrapper wrapper = new();
        HandleRef<int> reference = new(wrapper, 123);
        reference.Wrapper.Should().BeSameAs(wrapper);
        reference.Handle.Should().Be(123);
    }

    [Fact]
    public void Ctor_NullWrapper_AssignsNull()
    {
        HandleRef<int> reference = new(null, 42);
        reference.Wrapper.Should().BeNull();
        reference.Handle.Should().Be(42);
    }

    [Fact]
    public void Equals_TypedSameWrapperAndHandle_ReturnsTrue()
    {
        TestWrapper wrapper = new();
        HandleRef<int> a = new(wrapper, 5);
        HandleRef<int> b = new(wrapper, 5);
        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
    }

    [Fact]
    public void Equals_TypedDifferentHandle_ReturnsFalse()
    {
        TestWrapper wrapper = new();
        HandleRef<int> a = new(wrapper, 5);
        HandleRef<int> b = new(wrapper, 6);
        a.Equals(b).Should().BeFalse();
        (a == b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void Equals_TypedDifferentWrapper_ReturnsFalse()
    {
        HandleRef<int> a = new(new TestWrapper(), 5);
        HandleRef<int> b = new(new TestWrapper(), 5);
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_BoxedSameWrapperAndHandle_ReturnsTrue()
    {
        TestWrapper wrapper = new();
        HandleRef<int> a = new(wrapper, 5);
        object boxed = new HandleRef<int>(wrapper, 5);
        a.Equals(boxed).Should().BeTrue();
    }

    [Fact]
    public void Equals_BoxedDifferentHandle_ReturnsFalse()
    {
        TestWrapper wrapper = new();
        HandleRef<int> a = new(wrapper, 5);
        object boxed = new HandleRef<int>(wrapper, 6);
        a.Equals(boxed).Should().BeFalse();
    }

    [Fact]
    public void Equals_BoxedRawHandleValue_ReturnsFalse()
    {
        // Regression test: previously the override was `obj is THandle other`,
        // which would (a) never match a HandleRef and (b) be true for a bare
        // THandle with an unrelated comparison path. Both are wrong.
        HandleRef<int> a = new(null, 5);
        object boxedHandle = 5;
        a.Equals(boxedHandle).Should().BeFalse();
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        HandleRef<int> a = new(null, 5);
        a.Equals((object?)null).Should().BeFalse();
    }

    [Fact]
    public void Equals_DifferentType_ReturnsFalse()
    {
        HandleRef<int> a = new(null, 5);
        a.Equals("not a HandleRef").Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_EqualHandleRefs_AreEqual()
    {
        TestWrapper wrapper = new();
        HandleRef<int> a = new(wrapper, 5);
        HandleRef<int> b = new(wrapper, 5);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void IsNull_DefaultHandle_ReturnsTrue()
    {
        HandleRef<int> a = new(null, default);
        a.IsNull.Should().BeTrue();
    }

    [Fact]
    public void IsNull_NonDefaultHandle_ReturnsFalse()
    {
        HandleRef<int> a = new(null, 1);
        a.IsNull.Should().BeFalse();
    }
}
