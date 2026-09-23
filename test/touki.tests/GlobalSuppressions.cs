// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// IDE0380 correctly reports this unsafe context as unnecessary in modern .NET. The shared test
// helper must retain it for its .NET Framework build, which per-target analysis cannot account for.

#if NET

[assembly: SuppressMessage(
    "Style",
    "IDE0380:Remove unnecessary 'unsafe' modifier",
    Justification = "Required by the .NET Framework target of this multi-targeted build.",
    Scope = "type",
    Target = "~T:Touki.Resources.RawResourceReaderTests.NativeMemoryManager")]

#endif
