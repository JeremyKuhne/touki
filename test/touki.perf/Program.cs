// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace touki.perf;

internal class Program
{
    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--large-file")
        {
            LargeFileReadCommand.Run(args);
            return;
        }

        BenchmarkDotNet.Running.BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
