// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ;

namespace TraceQ.Core.Tests;

/// <summary>
///  M0 smoke tests: prove the self-contained scaffold compiles, the test runner
///  works, and the core assembly is referenceable. Real coverage of the readers,
///  providers, and engine arrives with the M1 asset copy.
/// </summary>
[TestClass]
public sealed class ScaffoldTests
{
    [TestMethod]
    public void TraceQCore_Milestone_IsReferenceable()
    {
        TraceQCore.Milestone.Should().Be("M1");
    }
}
