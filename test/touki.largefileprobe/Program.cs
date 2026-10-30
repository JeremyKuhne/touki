// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using touki.perf;

namespace touki.largefileprobe;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--runtime-info")
        {
            Console.WriteLine(
                $"{RuntimeInformation.FrameworkDescription}; DynamicCodeSupported={RuntimeFeature.IsDynamicCodeSupported};"
                    + $" DynamicCodeCompiled={RuntimeFeature.IsDynamicCodeCompiled}");

            return;
        }

        if (args.Length == 0 || args[0] != "--large-file")
        {
            throw new ArgumentException("Expected --runtime-info or --large-file followed by the shared replay arguments.");
        }

        LargeFileReadCommand.Run(args);
    }
}
