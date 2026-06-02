// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class ObjectDisposedExtensionsTests
{
    private sealed class Sample { }

    [Test]
    public void ThrowIf_FalseWithInstance_DoesNotThrow()
    {
        ObjectDisposedException.ThrowIf(false, new Sample());
    }

    [Test]
    public void ThrowIf_TrueWithInstance_ThrowsWithTypeName()
    {
        Action action = () => ObjectDisposedException.ThrowIf(true, new Sample());
        action.Should().Throw<ObjectDisposedException>()
            .Which.ObjectName.Should().Be(typeof(Sample).FullName);
    }

    [Test]
    public void ThrowIf_FalseWithType_DoesNotThrow()
    {
        ObjectDisposedException.ThrowIf(false, typeof(Sample));
    }

    [Test]
    public void ThrowIf_TrueWithType_ThrowsWithTypeName()
    {
        Action action = () => ObjectDisposedException.ThrowIf(true, typeof(Sample));
        action.Should().Throw<ObjectDisposedException>()
            .Which.ObjectName.Should().Be(typeof(Sample).FullName);
    }
}
