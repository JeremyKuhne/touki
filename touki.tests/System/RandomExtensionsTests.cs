// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

#pragma warning disable CA5394 // Random is insecure - test only

public class RandomExtensionsTests
{
    [Test]
    public void NextBytes_ExactRandomType_FillsBuffer()
    {
        Random r = new(42);
        Span<byte> buffer = stackalloc byte[64];
        r.NextBytes(buffer);

        bool anyNonZero = false;
        foreach (byte b in buffer)
        {
            if (b != 0)
            {
                anyNonZero = true;
                break;
            }
        }

        anyNonZero.Should().BeTrue();
    }

    [Test]
    public void NextBytes_ExactRandomType_DeterministicForSameSeed()
    {
        // Two Random instances with the same seed must fill spans identically.
        Random r1 = new(12345);
        Random r2 = new(12345);

        Span<byte> a = stackalloc byte[32];
        Span<byte> b = stackalloc byte[32];
        r1.NextBytes(a);
        r2.NextBytes(b);

        a.SequenceEqual(b).Should().BeTrue();
    }

    [Test]
    public void NextBytes_EmptySpan_NoOp()
    {
        Random r = new(1);
        r.NextBytes([]);
    }

#if NETFRAMEWORK
    [Test]
    public void NextBytes_NullRandom_Throws()
    {
        Random r = null!;
        Action a = () =>
        {
            Span<byte> buffer = stackalloc byte[4];
            r.NextBytes(buffer);
        };

        a.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void NextBytes_DerivedType_DispatchesToOverride()
    {
        // The polyfill forwards subclass calls through Random.NextBytes(byte[]) so any
        // override there observes the call. On modern .NET the BCL implements the span
        // overload independently and this fallback isn't exercised, so this test only
        // applies to the net481 polyfill.
        CountingRandom r = new(7);
        Span<byte> buffer = stackalloc byte[16];
        r.NextBytes(buffer);

        r.NextBytesArrayCalls.Should().Be(1);
    }

    private sealed class CountingRandom(int seed) : Random(seed)
    {
        public int NextBytesArrayCalls { get; private set; }

        public override void NextBytes(byte[] buffer)
        {
            NextBytesArrayCalls++;
            base.NextBytes(buffer);
        }
    }
#endif
}

