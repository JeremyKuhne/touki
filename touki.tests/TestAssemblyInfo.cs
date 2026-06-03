// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Match method-level parallelism. Workers = 0 lets MSTest use
// Environment.ProcessorCount worker threads; ExecutionScope.MethodLevel runs individual
// test methods (not just classes) in parallel. Classes marked [DoNotParallelize] opt out
// of the parallel pool.
[assembly: Parallelize(Workers = 0, Scope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope.MethodLevel)]

// Discover internal test classes and methods (TUnit discovered both public and internal).
[assembly: DiscoverInternals]
