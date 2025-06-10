// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.ComponentModel;

namespace Touki;

public class ComponentBaseTests
{
    private class TestContainer : Container
    {
        public bool RemoveCalled { get; private set; }

        public override void Remove(IComponent? component)
        {
            RemoveCalled = true;
            base.Remove(component);
        }
    }

    // Test helper for tracking Disposed event
    private class DisposedTracker
    {
        public bool DisposedCalled { get; private set; }
        public object? Sender { get; private set; }

        public void OnDisposed(object? sender, EventArgs e)
        {
            DisposedCalled = true;
            Sender = sender;
        }
    }

    [Fact]
    public void Dispose_RaisesDisposedEvent()
    {
        ComponentBase component = new();
        DisposedTracker tracker = new();

        ((IComponent)component).Disposed += tracker.OnDisposed;
        component.Dispose();

        tracker.DisposedCalled.Should().BeTrue();
        tracker.Sender.Should().BeSameAs(component);
    }

    [Fact]
    public void Dispose_MultipleCallsOnlyRaisesEventOnce()
    {
        ComponentBase component = new();
        int callCount = 0;

        ((IComponent)component).Disposed += (s, e) => callCount++;

        component.Dispose();
        component.Dispose();
        component.Dispose();

        callCount.Should().Be(1);
    }

    [Fact]
    public void Dispose_RemovesComponentFromContainer()
    {
        TestContainer container = new();
        ComponentBase component = new();

        container.Add(component);

        // Dispose the component
        component.Dispose();

        // Verify the component was removed from the container
        container.RemoveCalled.Should().BeTrue();
        container.Components.OfType<ComponentBase>().Should().NotContain(component);
    }

    [Fact]
    public void DisposedEvent_CanBeAddedAndRemoved()
    {
        ComponentBase component = new();
        DisposedTracker tracker = new();
        EventHandler handler = tracker.OnDisposed;

        // Add and then remove the handler
        ((IComponent)component).Disposed += handler;
        ((IComponent)component).Disposed -= handler;

        // Disposing shouldn't call the removed handler
        component.Dispose();

        tracker.DisposedCalled.Should().BeFalse();
    }

    [Fact]
    public void Dispose_WithNullSite_DoesNotThrow()
    {
        ComponentBase component = new();
        Action action = component.Dispose;
        action.Should().NotThrow();
    }

    [Fact]
    public void Dispose_NullDisposedHandler_DoesNotThrow()
    {
        ComponentBase component = new();

        // DisposedHandler is null by default
        Action action = component.Dispose;

        action.Should().NotThrow();
    }
}
