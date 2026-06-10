// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

[TestClass]
public sealed class TraceMetricSelectorTests
{
    [TestMethod]
    [DataRow("cpu", TraceMetric.Cpu)]
    [DataRow("threadtime", TraceMetric.ThreadTime)]
    [DataRow("alloc", TraceMetric.Allocations)]
    [DataRow("allocations", TraceMetric.Allocations)]
    [DataRow("exceptions", TraceMetric.Exceptions)]
    public void TryResolve_KnownSelector_ResolvesToProviderView(string selector, TraceMetric expected)
    {
        TraceMetricSelector.TryResolve(selector, out TraceMetric metric).Should().BeTrue();
        metric.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("CPU")]
    [DataRow("Alloc")]
    [DataRow("ThreadTime")]
    public void TryResolve_IsCaseInsensitive(string selector)
    {
        TraceMetricSelector.TryResolve(selector, out _).Should().BeTrue();
    }

    [TestMethod]
    public void TryResolve_UnknownSelector_ReturnsFalseAndDefaultsToCpu()
    {
        TraceMetricSelector.TryResolve("ipc", out TraceMetric metric).Should().BeFalse();
        metric.Should().Be(TraceMetric.Cpu);
    }

    [TestMethod]
    public void Selectors_ListsTheCanonicalNames()
    {
        TraceMetricSelector.Selectors.Should().Equal("cpu", "threadtime", "alloc", "exceptions");
    }
}
