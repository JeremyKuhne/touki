// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from the xUnit.net v3 RetryFactExample sample.
// https://github.com/xunit/samples.xunit/tree/main/v3/RetryFactExample

using System.ComponentModel;
using Xunit.Sdk;
using Xunit.v3;

namespace Xunit;

/// <summary>
///  Test case backing a single <see cref="RetryFactAttribute"/>-annotated method.
/// </summary>
public class RetryTestCase : XunitTestCase, ISelfExecutingXunitTestCase
{
    /// <summary>
    ///  Parameterless constructor required by the xUnit deserializer.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
    public RetryTestCase()
    {
    }

    /// <summary>
    ///  Creates a new retry-capable test case.
    /// </summary>
    public RetryTestCase(
        int maxRetries,
        IXunitTestMethod testMethod,
        string testCaseDisplayName,
        string uniqueID,
        bool @explicit,
        Type[]? skipExceptions = null,
        string? skipReason = null,
        Type? skipType = null,
        string? skipUnless = null,
        string? skipWhen = null,
        Dictionary<string, HashSet<string>>? traits = null,
        object?[]? testMethodArguments = null,
        string? sourceFilePath = null,
        int? sourceLineNumber = null,
        int? timeout = null)
        : base(
            testMethod,
            testCaseDisplayName,
            uniqueID,
            @explicit,
            skipExceptions,
            skipReason,
            skipType,
            skipUnless,
            skipWhen,
            traits,
            testMethodArguments,
            sourceFilePath,
            sourceLineNumber,
            timeout)
    {
        MaxRetries = maxRetries;
    }

    /// <summary>
    ///  Maximum number of times this case will be executed before reporting failure.
    /// </summary>
    public int MaxRetries { get; private set; }

    /// <inheritdoc/>
    protected override void Deserialize(IXunitSerializationInfo info)
    {
        base.Deserialize(info);
        MaxRetries = info.GetValue<int>(nameof(MaxRetries));
    }

    /// <inheritdoc/>
    public ValueTask<RunSummary> Run(
        ExplicitOption explicitOption,
        IMessageBus messageBus,
        object?[] constructorArguments,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource) =>
            RetryTestCaseRunner.Instance.Run(
                MaxRetries,
                this,
                messageBus,
                aggregator.Clone(),
                cancellationTokenSource,
                TestCaseDisplayName,
                SkipReason,
                explicitOption,
                constructorArguments);

    /// <inheritdoc/>
    protected override void Serialize(IXunitSerializationInfo info)
    {
        base.Serialize(info);
        info.AddValue(nameof(MaxRetries), MaxRetries);
    }
}
