// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;

namespace Touki.Analyzers;

public sealed partial class RequireNamedArgumentsForLiteralsAnalyzer
{
    [Flags]
    private enum LiteralKinds
    {
        None = 0,
        Integer = 1 << 0,
        FloatingPoint = 1 << 1,
        Character = 1 << 2,
        String = 1 << 3,
        Boolean = 1 << 4,
        Null = 1 << 5,
        Default = 1 << 6
    }
}