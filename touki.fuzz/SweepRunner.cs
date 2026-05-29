// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;

namespace Touki.Fuzz;

/// <summary>
///  Deterministic, in-process random sweep over a fuzz target. Requires no native fuzzing tooling.
/// </summary>
/// <remarks>
///  <para>
///   Generates pseudo-random byte inputs of varying lengths from a fixed seed and invokes the target,
///   reporting any unhandled exception (including <see cref="FuzzInvariantException"/>) along with the
///   exact input that triggered it. Because the seed is fixed, a failing input reproduces on every run.
///  </para>
///  <para>
///   A watchdog detects iterations that fail to make progress (for example an infinite loop) and reports
///   the offending input instead of hanging.
///  </para>
///  <para>
///   Tuning is via environment variables: <c>FUZZ_ITERATIONS</c> (default 2,000,000),
///   <c>FUZZ_MAX_LENGTH</c> (default 64), <c>FUZZ_SEED</c> (default 1), and
///   <c>FUZZ_TIMEOUT_MS</c> (default 5,000) for the watchdog stall threshold.
///  </para>
/// </remarks>
internal static class SweepRunner
{
    internal static int Run(string target, ReadOnlySpanAction action)
    {
        int iterations = GetInt("FUZZ_ITERATIONS", 2_000_000);
        int maxLength = GetInt("FUZZ_MAX_LENGTH", 64);
        int seed = GetInt("FUZZ_SEED", 1);
        int timeoutMs = GetInt("FUZZ_TIMEOUT_MS", 5_000);

        Console.WriteLine($"Sweep '{target}': iterations={iterations}, maxLength={maxLength}, seed={seed}");

        byte[] lastInput = new byte[maxLength];
        int lastLength = 0;
        int counter = -1;

        Exception? crash = null;
        int crashIteration = -1;
        byte[] crashInput = [];

        Thread worker = new(() =>
        {
            Random random = new(seed);
            byte[] buffer = new byte[maxLength];

            for (int i = 0; i < iterations; i++)
            {
                // A fixed-seed System.Random is required so a failing input reproduces deterministically.
                // This is test-input generation, not a security-sensitive use.
#pragma warning disable CA5394 // Do not use insecure randomness
                int length = random.Next(0, maxLength + 1);
                random.NextBytes(buffer.AsSpan(0, length));
#pragma warning restore CA5394

                // Snapshot the current input so the watchdog can report it if this iteration stalls.
                buffer.AsSpan(0, length).CopyTo(lastInput);
                Volatile.Write(ref lastLength, length);
                Volatile.Write(ref counter, i);

                try
                {
                    action(buffer.AsSpan(0, length));
                }
                catch (Exception ex)
                {
                    crash = ex;
                    crashIteration = i;
                    crashInput = buffer.AsSpan(0, length).ToArray();
                    return;
                }
            }

            Volatile.Write(ref counter, iterations);
        })
        {
            IsBackground = true,
            Name = "fuzz-sweep"
        };

        worker.Start();

        while (!worker.Join(timeoutMs))
        {
            int before = Volatile.Read(ref counter);
            Thread.Sleep(50);

            if (Volatile.Read(ref counter) == before && !worker.Join(0))
            {
                int len = Volatile.Read(ref lastLength);
                Console.WriteLine($"HANG at iteration {before} (seed {seed}). No progress for >= {timeoutMs} ms.");
                Console.WriteLine($"  Input ({len} bytes): {ToHex(lastInput.AsSpan(0, len))}");
                Console.Out.Flush();
                Environment.Exit(2);
            }
        }

        if (crash is not null)
        {
            Console.WriteLine($"CRASH at iteration {crashIteration} (seed {seed}).");
            Console.WriteLine($"  Input ({crashInput.Length} bytes): {ToHex(crashInput)}");
            Console.WriteLine($"  {crash.GetType().FullName}: {crash.Message}");
            Console.WriteLine(crash.StackTrace);
            return 1;
        }

        Console.WriteLine($"Sweep '{target}' completed cleanly ({iterations} iterations).");
        return 0;
    }

    private static int GetInt(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out int value) ? value : fallback;

    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return "<empty>";
        }

        StringBuilder builder = new(bytes.Length * 2);
        foreach (byte b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }
}
