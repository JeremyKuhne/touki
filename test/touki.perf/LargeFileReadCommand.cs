// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Diagnostics;
using System.Globalization;

namespace touki.perf;

internal static class LargeFileReadCommand
{
    internal static void Run(string[] args)
    {
        if (args.Length == 2 && args[1] == "purge-system-cache")
        {
#if NET
            if (!OperatingSystem.IsWindowsVersionAtLeast(10))
            {
                throw new PlatformNotSupportedException("System file-cache purging requires Windows 10 or later.");
            }
#endif

            LargeFileCacheControl.PurgeSystemCacheAndStandby();
            return;
        }

        if (args.Length == 2 && args[1] == "purge")
        {
#if NET
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Standby-list purging requires Windows.");
            }
#endif

            LargeFileCacheControl.PurgeStandbyList();
            return;
        }

        if (args.Length == 5 && args[1] == "generate")
        {
            bool blocks = ParsePattern(args[4]);
            LargeFileReadPattern.Create(args[2], long.Parse(args[3], CultureInfo.InvariantCulture), blocks);
            return;
        }

        if (args.Length == 6 && args[1] == "profile")
        {
            LargeFileReadProfile.Run(
                args[2],
                (LargeFileReadReference)Enum.Parse(typeof(LargeFileReadReference), args[3]),
                (LargeFileReadScenario)Enum.Parse(typeof(LargeFileReadScenario), args[4]),
                int.Parse(args[5], CultureInfo.InvariantCulture));

            return;
        }

#if NET
        if (args.Length == 6 && args[1] == "profile-async")
        {
            LargeFileReadProfile.RunAsync(
                args[2],
                (LargeFileReadReference)Enum.Parse(typeof(LargeFileReadReference), args[3]),
                (LargeFileReadScenario)Enum.Parse(typeof(LargeFileReadScenario), args[4]),
                int.Parse(args[5], CultureInfo.InvariantCulture)).GetAwaiter().GetResult();

            return;
        }
#endif

        if (args.Length is 4 or 5 && args[1] == "verify")
        {
            LargeFileReadReference verifyReference = args.Length == 4
                ? LargeFileReadReference.TunedFileStream
                : (LargeFileReadReference)Enum.Parse(typeof(LargeFileReadReference), args[4]);

            LargeFileReadPattern.Verify(args[2], ParsePattern(args[3]), verifyReference);
            return;
        }

        if (args.Length == 5
            && args[1] == "run"
            && Enum.TryParse(args[3], out LargeFileReadReference reference)
            && Enum.IsDefined(typeof(LargeFileReadReference), reference)
            && Enum.TryParse(args[4], out LargeFileReadScenario scenario)
            && Enum.IsDefined(typeof(LargeFileReadScenario), scenario))
        {
            Measure(args[2], reference, scenario);
            return;
        }

        throw new ArgumentException(
            "--large-file: generate <path> <bytes> <pattern|blocks>; verify <path> <pattern|blocks> [reference];"
                + " run <path> <reference> <scenario>; profile <path> <reference> <scenario> <iterations>;"
#if NET
                + " profile-async <path> <reference> <scenario> <iterations>;"
#endif
                + " purge; purge-system-cache");
    }

    private static bool ParsePattern(string value) => value switch
    {
        "pattern" => false,
        "blocks" => true,
        _ => throw new ArgumentException("Expected pattern or blocks.", nameof(value))
    };

    private static void Measure(string path, LargeFileReadReference reference, LargeFileReadScenario scenario)
    {
        using Process process = Process.GetCurrentProcess();
        TimeSpan cpu = process.TotalProcessorTime;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        long checksum;
        long bytes;
        long requests;
        long length;
        int capacity;
        double firstByte;
        using (LargeFileReadReferenceReader reader = new(path, reference, scenario))
        {
            checksum = LargeFileReadReplay.Run(reader, scenario);
            bytes = reader.BackendBytesRead;
            requests = reader.RequestsIssued;
            length = reader.Length;
            capacity = reader.Capacity + LargeFileReadPattern.WindowSize;
            firstByte = reader.FirstByteTimestamp == 0
                ? 0
                : (reader.FirstByteTimestamp - started) * 1000.0 / Stopwatch.Frequency;
        }

        double elapsed = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        double cpuMilliseconds = (process.TotalProcessorTime - cpu).TotalMilliseconds;
        Console.WriteLine(string.Join(
            ",",
            length.ToString(CultureInfo.InvariantCulture),
            elapsed.ToString("F4", CultureInfo.InvariantCulture),
            cpuMilliseconds.ToString("F4", CultureInfo.InvariantCulture),
            allocated.ToString(CultureInfo.InvariantCulture),
            bytes.ToString(CultureInfo.InvariantCulture),
            requests.ToString(CultureInfo.InvariantCulture),
            capacity.ToString(CultureInfo.InvariantCulture),
            firstByte.ToString("F4", CultureInfo.InvariantCulture),
            checksum.ToString(CultureInfo.InvariantCulture)));
    }
}
