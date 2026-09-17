// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Interop;

[TestClass]
public class HandleRefTests
{
    private sealed class TestWrapper
    {
    }

    private sealed class TestHandleProvider : IHandle<int>
    {
        public int Handle { get; init; }

        public object? Wrapper => this;
    }

    [TestMethod]
    public void Ctor_WrapperAndHandle_AssignsBoth()
    {
        TestWrapper wrapper = new();
        HandleRef<int> reference = new(wrapper, 123);
        reference.Wrapper.Should().BeSameAs(wrapper);
        reference.Handle.Should().Be(123);
    }

    [TestMethod]
    public void Ctor_NullWrapper_AssignsNull()
    {
        HandleRef<int> reference = new(null, 42);
        reference.Wrapper.Should().BeNull();
        reference.Handle.Should().Be(42);
    }

    [TestMethod]
    public void Ctor_FromIHandle_AssignsWrapperAndHandle()
    {
        TestHandleProvider provider = new() { Handle = 99 };
        HandleRef<int> reference = new(provider);
        reference.Wrapper.Should().BeSameAs(provider);
        reference.Handle.Should().Be(99);
    }

    [TestMethod]
    public void Ctor_FromNullIHandle_HandleDefaultsToZero()
    {
        HandleRef<int> reference = new((IHandle<int>?)null);
        reference.Wrapper.Should().BeNull();
        reference.Handle.Should().Be(0);
    }

    [TestMethod]
    public void Equals_TypedSameWrapperAndHandle_ReturnsTrue()
    {
        TestWrapper wrapper = new();
        HandleRef<int> a = new(wrapper, 5);
        HandleRef<int> b = new(wrapper, 5);
        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
    }

    [TestMethod]
    public void Equals_TypedDifferentHandle_ReturnsFalse()
    {
        TestWrapper wrapper = new();
        HandleRef<int> a = new(wrapper, 5);
        HandleRef<int> b = new(wrapper, 6);
        a.Equals(b).Should().BeFalse();
        (a == b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }

    [TestMethod]
    public void Equals_TypedDifferentWrapper_ReturnsFalse()
    {
        HandleRef<int> a = new(new TestWrapper(), 5);
        HandleRef<int> b = new(new TestWrapper(), 5);
        a.Equals(b).Should().BeFalse();
    }

    [TestMethod]
    public void Equals_BoxedSameWrapperAndHandle_ReturnsTrue()
    {
        TestWrapper wrapper = new();
        HandleRef<int> a = new(wrapper, 5);
        object boxed = new HandleRef<int>(wrapper, 5);
        a.Equals(boxed).Should().BeTrue();
    }

    [TestMethod]
    public void Equals_BoxedDifferentHandle_ReturnsFalse()
    {
        TestWrapper wrapper = new();
        HandleRef<int> a = new(wrapper, 5);
        object boxed = new HandleRef<int>(wrapper, 6);
        a.Equals(boxed).Should().BeFalse();
    }

    [TestMethod]
    public void Equals_BoxedRawHandleValue_ReturnsFalse()
    {
        // Regression test: previously the override was `obj is THandle other`,
        // which would (a) never match a HandleRef and (b) be true for a bare
        // THandle with an unrelated comparison path. Both are wrong.
        HandleRef<int> a = new(null, 5);
        object boxedHandle = 5;
        a.Equals(boxedHandle).Should().BeFalse();
    }

    [TestMethod]
    public void Equals_Null_ReturnsFalse()
    {
        HandleRef<int> a = new(null, 5);
        a.Equals((object?)null).Should().BeFalse();
    }

    [TestMethod]
    public void Equals_DifferentType_ReturnsFalse()
    {
        HandleRef<int> a = new(null, 5);
        a.Equals("not a HandleRef").Should().BeFalse();
    }

    [TestMethod]
    public void GetHashCode_EqualHandleRefs_AreEqual()
    {
        TestWrapper wrapper = new();
        HandleRef<int> a = new(wrapper, 5);
        HandleRef<int> b = new(wrapper, 5);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [TestMethod]
    public void IsNull_DefaultHandle_ReturnsTrue()
    {
        HandleRef<int> a = new(null, default);
        a.IsNull.Should().BeTrue();
    }

    [TestMethod]
    public void IsNull_NonDefaultHandle_ReturnsFalse()
    {
        HandleRef<int> a = new(null, 1);
        a.IsNull.Should().BeFalse();
    }
}
