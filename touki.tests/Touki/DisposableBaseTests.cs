// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class DisposableBaseTests
{
    // Test implementation of DisposableBase
    private class TestDisposable : DisposableBase
    {
        public int DisposeCallCount { get; private set; }
        public bool LastDisposeParameter { get; private set; }
        public bool ThrowOnDispose { get; set; }

        public bool IsDisposed => Disposed;

        protected override void Dispose(bool disposing)
        {
            DisposeCallCount++;
            LastDisposeParameter = disposing;

            if (ThrowOnDispose)
            {
                throw new InvalidOperationException("Dispose test exception");
            }
        }
    }

    // Test implementation of DisposableBase.Finalizable
    private class TestFinalizableDisposable : DisposableBase.Finalizable
    {
        public int DisposeCallCount { get; private set; }
        public bool LastDisposeParameter { get; private set; }
        public bool IsDisposed => Disposed;

        protected override void Dispose(bool disposing)
        {
            DisposeCallCount++;
            LastDisposeParameter = disposing;
        }
    }

    [Fact]
    public void Disposed_ReturnsFalse_WhenNotDisposed()
    {
        TestDisposable disposable = new();

        disposable.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void Disposed_ReturnsTrue_AfterDisposal()
    {
        TestDisposable disposable = new();
        disposable.Dispose();
        disposable.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_CallsDisposeMethod_WithTrueParameter()
    {
        TestDisposable disposable = new();

        disposable.Dispose();

        disposable.DisposeCallCount.Should().Be(1);
        disposable.LastDisposeParameter.Should().BeTrue();
    }

    [Fact]
    public void Dispose_DoesNotCallDisposeMethodTwice_WhenCalledMultipleTimes()
    {
        TestDisposable disposable = new();

        disposable.Dispose();
        disposable.Dispose();
        disposable.Dispose();

        disposable.DisposeCallCount.Should().Be(1);
    }

    [Fact]
    public void Dispose_StillMarksAsDisposed_WhenDisposeThrowsException()
    {
        TestDisposable disposable = new() { ThrowOnDispose = true };

        disposable.Invoking(d => d.Dispose()).Should().Throw<InvalidOperationException>();
        disposable.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Finalize_MarksAsDisposed()
    {
        TestFinalizableDisposable disposable = new();
        TestHelper.InvokeFinalizer(disposable);
        disposable.IsDisposed.Should().BeTrue();
        disposable.DisposeCallCount.Should().Be(1);
        disposable.LastDisposeParameter.Should().BeFalse();
    }

    [Fact]
    public void Dispose_AfterFinalization_DoesNothing()
    {
        TestFinalizableDisposable disposable = new();
        TestHelper.InvokeFinalizer(disposable);

        // After finalization, calling Dispose should not throw or change state
        disposable.Invoking(d => d.Dispose()).Should().NotThrow();
        disposable.IsDisposed.Should().BeTrue();
        disposable.DisposeCallCount.Should().Be(1);
        disposable.LastDisposeParameter.Should().BeFalse();
    }

    // Helper method to make DisposeInternal accessible for testing
    private static class ReflectionHelper
    {
        public static void Finalize(DisposableBase disposable, bool disposing)
        {
            var method = typeof(DisposableBase).GetMethod(
                "DisposeInternal",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            method?.Invoke(disposable, [disposing]);
        }
    }
}
