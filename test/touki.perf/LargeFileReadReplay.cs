// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Buffers.Binary;
#if NET
#pragma warning disable IDE0005 // Also linked into touki.tests, whose global usings differ.
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
#pragma warning restore IDE0005
#endif

namespace touki.perf;

internal static class LargeFileReadReplay
{
    internal static long Run(LargeFileReadReferenceReader reader, LargeFileReadScenario scenario)
    {
        byte[] buffer = new byte[LargeFileReadPattern.WindowSize];
        long checksum = 0;
        foreach ((long offset, int count, bool parse) in Requests(reader.Length, scenario))
        {
            reader.ReadExactly(offset, buffer, count);
            checksum = Digest(buffer, count, checksum, parse);
        }

        return checksum;
    }

#if NET
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    internal static async ValueTask<long> RunAsync(
        LargeFileReadReferenceReader reader,
        LargeFileReadScenario scenario,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] buffer = new byte[LargeFileReadPattern.WindowSize];
        long checksum = 0;
        foreach ((long offset, int count, bool parse) in Requests(reader.Length, scenario))
        {
            await reader.ReadExactlyAsync(offset, buffer, count, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            checksum = Digest(buffer, count, checksum, parse);
        }

        return checksum;
    }
#endif

    private static IEnumerable<(long Offset, int Count, bool Parse)> Requests(long length, LargeFileReadScenario scenario)
    {
        if (scenario == LargeFileReadScenario.Sparse)
        {
            ulong state = 0xBAD5EED;
            int count = (int)Math.Min(72, length);
            if (count == 0)
            {
                yield break;
            }

            for (int i = 0; i < 4096; i++)
            {
                state = unchecked(state * 6364136223846793005UL + 1);
                long offset = (long)(state % (ulong)(length - count + 1));
                yield return (offset, count, false);
            }

            yield break;
        }

        long end = scenario == LargeFileReadScenario.EarlyStop ? length / 100 : length;
        for (long offset = 0; offset < end;)
        {
            int count = (int)Math.Min(
                scenario is LargeFileReadScenario.ScriptedSequential or LargeFileReadScenario.BlockScan
                    ? LargeFileReadPattern.BlockSize
                    : LargeFileReadPattern.WindowSize,
                end - offset);

            if (scenario == LargeFileReadScenario.ScriptedSequential)
            {
                int header = Math.Min(72, count);
                yield return (offset, header, false);
                yield return (offset + header, count - header, false);
                yield return (offset, header, false);
            }
            else
            {
                yield return (offset, count, scenario == LargeFileReadScenario.BlockScan);
            }

            offset += count;
        }
    }

    private static long Digest(byte[] buffer, int count, long checksum, bool parse)
    {
        for (int i = 0; i < count; i += 4096)
        {
            checksum = Mix(buffer, i, Math.Min(sizeof(long), count - i), checksum);
        }

        if (count != 0)
        {
            checksum = unchecked(checksum * 31 + buffer[count - 1]);
        }

        if (parse)
        {
            for (int i = 0; i < count; i += sizeof(long))
            {
                checksum = Mix(buffer, i, Math.Min(sizeof(long), count - i), checksum);
            }
        }

        return checksum;
    }

    private static long Mix(byte[] buffer, int start, int count, long checksum)
    {
        if (count == sizeof(long))
        {
            return unchecked(checksum * 31 + BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(start, count)));
        }

        for (int i = 0; i < count; i++)
        {
            checksum = unchecked(checksum * 31 + buffer[start + i]);
        }

        return checksum;
    }
}
