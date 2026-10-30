// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace touki.perf;

public enum LargeFileReadScenario
{
    Sequential,
    ScriptedSequential,
    Sparse,
    BlockScan,
    EarlyStop
}
