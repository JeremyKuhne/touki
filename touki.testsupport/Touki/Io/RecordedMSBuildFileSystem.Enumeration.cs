// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

public sealed partial class RecordedMSBuildFileSystem
{
    private readonly struct Enumeration
    {
        public Enumeration(string method, string path, string pattern, int option, string[] results)
        {
            Method = method;
            Path = path;
            Pattern = pattern;
            Option = option;
            Results = results;
        }

        public string Method { get; }
        public string Path { get; }
        public string Pattern { get; }
        public int Option { get; }
        public string[] Results { get; }
    }
}
