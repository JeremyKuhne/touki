// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.ValueTests;

public class StoringObject
{
    [Fact]
    public void BasicStorage()
    {
        A a = new();
        Value value = Value.Create(a);
        Assert.Equal(typeof(A), value.Type);
        Assert.Same(a, value.As<A>());

        bool success = value.TryGetValue(out B result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DerivedRetrieval()
    {
        B b = new();
        Value value = Value.Create(b);
        Assert.Equal(typeof(B), value.Type);
        Assert.Same(b, value.As<A>());
        Assert.Same(b, value.As<B>());

        bool success = value.TryGetValue(out C result);
        Assert.False(success);
        Assert.Null(result);

        Assert.Throws<InvalidCastException>(() => value.As<C>());

        A a = new B();
        value = Value.Create(a);
        Assert.Equal(typeof(B), value.Type);
    }

    [Fact]
    public void AsInterface()
    {
        I a = new A();
        Value value = Value.Create(a);
        Assert.Equal(typeof(A), value.Type);

        Assert.Same(a, value.As<A>());
        Assert.Same(a, value.As<I>());
    }

    private class A : I { }
    private class B : A, I { }
    private class C : B, I { }

    private interface I
    {
        string? ToString();
    }
}
