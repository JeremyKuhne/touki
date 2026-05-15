// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from the xUnit.net v3 RetryFactExample sample.
// https://github.com/xunit/samples.xunit/tree/main/v3/RetryFactExample

using Xunit.v3;

namespace Xunit;

/// <summary>
///  A <see cref="FactAttribute"/> that re-runs a failing test until it passes or until
///  <see cref="MaxRetries"/> total attempts have been made. Use sparingly - for tests
///  that exercise system-wide resources (clipboard, network ports, file system races)
///  where transient host contention can produce false failures even when the code under
///  test is correct.
/// </summary>
[XunitTestCaseDiscoverer(typeof(RetryFactDiscoverer))]
public class RetryFactAttribute(
    [CallerFilePath] string? sourceFilePath = null,
    [CallerLineNumber] int sourceLineNumber = -1)
    : FactAttribute(sourceFilePath, sourceLineNumber)
{
    /// <summary>
    ///  Maximum number of total attempts (initial run plus retries) before reporting
    ///  failure. The initial run counts toward this limit; a value of <c>3</c> means the
    ///  test will be run up to three times, equivalent to two retries after the first
    ///  failure. Defaults to <c>3</c>. Values less than <c>1</c> are treated as <c>3</c>.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}
