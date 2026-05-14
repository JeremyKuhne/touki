// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from the xUnit.net v3 RetryFactExample sample.
// https://github.com/xunit/samples.xunit/tree/main/v3/RetryFactExample

using Xunit.v3;

namespace Xunit;

/// <summary>
///  A <see cref="FactAttribute"/> that re-runs a failing test up to <see cref="MaxRetries"/>
///  times. Use sparingly - for tests that exercise system-wide resources (clipboard,
///  network ports, file system races) where transient host contention can produce false
///  failures even when the code under test is correct.
/// </summary>
[XunitTestCaseDiscoverer(typeof(RetryFactDiscoverer))]
public class RetryFactAttribute(
    [CallerFilePath] string? sourceFilePath = null,
    [CallerLineNumber] int sourceLineNumber = -1)
    : FactAttribute(sourceFilePath, sourceLineNumber)
{
    /// <summary>
    ///  Maximum number of times to run the test before reporting failure. Defaults to 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}
