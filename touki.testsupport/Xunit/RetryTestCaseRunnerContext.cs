// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from the xUnit.net v3 RetryFactExample sample.
// https://github.com/xunit/samples.xunit/tree/main/v3/RetryFactExample

using Xunit.Sdk;
using Xunit.v3;

namespace Xunit;

/// <summary>
///  Context passed to <see cref="RetryTestCaseRunner"/> while executing a
///  <see cref="RetryTestCase"/>.
/// </summary>
public class RetryTestCaseRunnerContext(
    int maxRetries,
    IXunitTestCase testCase,
    IReadOnlyCollection<IXunitTest> tests,
    IMessageBus messageBus,
    ExceptionAggregator aggregator,
    CancellationTokenSource cancellationTokenSource,
    string displayName,
    string? skipReason,
    ExplicitOption explicitOption,
    object?[] constructorArguments) :
        XunitTestCaseRunnerBaseContext<IXunitTestCase, IXunitTest>(
            testCase,
            tests,
            messageBus,
            aggregator,
            cancellationTokenSource,
            displayName,
            skipReason,
            explicitOption,
            constructorArguments)
{
    /// <summary>
    ///  Maximum number of times the test will be run before reporting failure.
    /// </summary>
    public int MaxRetries { get; } = maxRetries;
}
