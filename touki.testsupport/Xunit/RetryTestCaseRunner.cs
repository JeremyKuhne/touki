// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from the xUnit.net v3 RetryFactExample sample.
// https://github.com/xunit/samples.xunit/tree/main/v3/RetryFactExample

using Xunit.Sdk;
using Xunit.v3;

namespace Xunit;

/// <summary>
///  Runner that executes a <see cref="RetryTestCase"/>, retrying transient failures.
/// </summary>
public class RetryTestCaseRunner
    : XunitTestCaseRunnerBase<RetryTestCaseRunnerContext, IXunitTestCase, IXunitTest>
{
    /// <summary>
    ///  Process-wide singleton runner instance.
    /// </summary>
    public static RetryTestCaseRunner Instance { get; } = new();

    /// <summary>
    ///  Discovers the tests for <paramref name="testCase"/> and invokes
    ///  <see cref="RunTest(RetryTestCaseRunnerContext, IXunitTest)"/> for each.
    /// </summary>
    public async ValueTask<RunSummary> Run(
        int maxRetries,
        IXunitTestCase testCase,
        IMessageBus messageBus,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        string displayName,
        string? skipReason,
        ExplicitOption explicitOption,
        object?[] constructorArguments)
    {
        // Centralized from XunitRunnerHelper.RunXunitTestCase so we do not duplicate
        // the discovery / skip / fail handling logic on every retry attempt.
        IReadOnlyCollection<IXunitTest> tests = await aggregator.RunAsync(testCase.CreateTests, []).ConfigureAwait(false);

        if (aggregator.ToException() is Exception ex)
        {
            if (ex.Message.StartsWith(DynamicSkipToken.Value, StringComparison.Ordinal))
            {
                return XunitRunnerHelper.SkipTestCases(
                    messageBus,
                    cancellationTokenSource,
                    [testCase],
                    ex.Message[DynamicSkipToken.Value.Length..],
                    sendTestCaseMessages: false);
            }

            return XunitRunnerHelper.FailTestCases(
                messageBus,
                cancellationTokenSource,
                [testCase],
                ex,
                sendTestCaseMessages: false);
        }

        // `await using` does not surface a hook for ConfigureAwait, so the dispose is
        // done manually inside a try / finally to satisfy CA2007.
        RetryTestCaseRunnerContext ctxt = new(
            maxRetries,
            testCase,
            tests,
            messageBus,
            aggregator,
            cancellationTokenSource,
            displayName,
            skipReason,
            explicitOption,
            constructorArguments);
        try
        {
            await ctxt.InitializeAsync().ConfigureAwait(false);

            return await Run(ctxt).ConfigureAwait(false);
        }
        finally
        {
            await ctxt.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    protected override async ValueTask<RunSummary> RunTest(
        RetryTestCaseRunnerContext ctxt,
        IXunitTest test)
    {
        int runCount = 0;
        int maxRetries = ctxt.MaxRetries;

        if (maxRetries < 1)
        {
            maxRetries = 3;
        }

        while (true)
        {
            // Capture and delay messages (which carry the run status) until we decide
            // whether to accept this attempt as the final result.
            DelayedMessageBus delayedMessageBus = new(ctxt.MessageBus);
            ExceptionAggregator aggregator = ctxt.Aggregator.Clone();
            RunSummary result = await XunitTestRunner.Instance.Run(
                test,
                delayedMessageBus,
                ctxt.ConstructorArguments,
                ctxt.ExplicitOption,
                aggregator,
                ctxt.CancellationTokenSource,
                ctxt.BeforeAfterTestAttributes).ConfigureAwait(false);

            if (!(aggregator.HasExceptions || result.Failed != 0) || ++runCount >= maxRetries)
            {
                // Flush all delayed messages so the runner sees the final result.
                delayedMessageBus.Dispose();
                return result;
            }

            TestContext.Current.SendDiagnosticMessage(
                "Execution of '{0}' failed (attempt #{1}), retrying...",
                test.TestDisplayName,
                runCount);

            ctxt.Aggregator.Clear();
        }
    }
}
