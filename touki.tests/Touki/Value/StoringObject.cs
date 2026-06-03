// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class StoringObject
{
    [TestMethod]
    public void BasicStorage()
    {
        A a = new();
        Value value = Value.Create(a);
        value.Type.Should().Be(typeof(A));
        value.As<A>().Should().BeSameAs(a);

        bool success = value.TryGetValue(out B result);
        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [TestMethod]
    public void DerivedRetrieval()
    {
        B b = new();
        Value value = Value.Create(b);
        value.Type.Should().Be(typeof(B));
        value.As<A>().Should().BeSameAs(b);
        value.As<B>().Should().BeSameAs(b);

        bool success = value.TryGetValue(out C result);
        success.Should().BeFalse();
        result.Should().BeNull();

        Assert.Throws<InvalidCastException>(() => value.As<C>());

        A a = new B();
        value = Value.Create(a);
        value.Type.Should().Be(typeof(B));
    }

    [TestMethod]
    public void AsInterface()
    {
        I a = new A();
        Value value = Value.Create(a);
        value.Type.Should().Be(typeof(A));

        value.As<A>().Should().BeSameAs(a);
        value.As<I>().Should().BeSameAs(a);
    }

    private class A : I { }
    private class B : A, I { }
    private class C : B, I { }

    private interface I
    {
        string? ToString();
    }

    [TestMethod]
    public void Box_StoresObject()
    {
        A a = new();
        Value value = Value.Box(a);
        value.As<A>().Should().BeSameAs(a);
    }

    [TestMethod]
    public void Box_NullObject_HasNoStoredValue()
    {
        // Per Value docs, a Type of null means "no value is stored".
        Value value = Value.Box(null);
        value.Type.Should().BeNull();
        Assert.Throws<InvalidCastException>(() => value.As<object>());
    }
}
