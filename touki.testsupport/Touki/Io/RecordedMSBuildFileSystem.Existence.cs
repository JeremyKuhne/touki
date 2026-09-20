// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

public sealed partial class RecordedMSBuildFileSystem
{
    private readonly struct Existence
    {
        public Existence(string method, string path, bool value)
        {
            Method = method;
            Path = path;
            Value = value;
        }

        public string Method { get; }
        public string Path { get; }
        public bool Value { get; }
    }
}
