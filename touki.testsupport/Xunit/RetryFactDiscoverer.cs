// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from the xUnit.net v3 RetryFactExample sample.
// https://github.com/xunit/samples.xunit/tree/main/v3/RetryFactExample

using Xunit.Internal;
using Xunit.Sdk;
using Xunit.v3;

namespace Xunit;

/// <summary>
///  Discoverer that produces a <see cref="RetryTestCase"/> for every
///  <see cref="RetryFactAttribute"/>-annotated method.
/// </summary>
public class RetryFactDiscoverer : IXunitTestCaseDiscoverer
{
    /// <inheritdoc/>
    public ValueTask<IReadOnlyCollection<IXunitTestCase>> Discover(
        ITestFrameworkDiscoveryOptions discoveryOptions,
        IXunitTestMethod testMethod,
        IFactAttribute factAttribute)
    {
        int maxRetries = (factAttribute as RetryFactAttribute)?.MaxRetries ?? 3;

        // TestIntrospectionHelper.GetTestCaseDetails returns a ValueTuple containing
        // the data needed to construct an XunitTestCase. The element names match the
        // GetTestCaseDetails out-tuple in the xUnit v3 source; we capture sourceFilePath
        // and sourceLineNumber so the runner can navigate to the failing test even
        // though XunitTestCase also tracks them via IXunitTestMethod.
        (
            string testCaseDisplayName,
            bool @explicit,
            Type[]? skipExceptions,
            string? skipReason,
            Type? skipType,
            string? skipUnless,
            string? skipWhen,
            string? sourceFilePath,
            int? sourceLineNumber,
            int? timeout,
            string uniqueID,
            IXunitTestMethod resolvedTestMethod) =
                TestIntrospectionHelper.GetTestCaseDetails(discoveryOptions, testMethod, factAttribute);

        RetryTestCase testCase = new(
            maxRetries,
            resolvedTestMethod,
            testCaseDisplayName,
            uniqueID,
            @explicit,
            skipExceptions,
            skipReason,
            skipType,
            skipUnless,
            skipWhen,
            testMethod.Traits.ToReadWrite(StringComparer.OrdinalIgnoreCase),
            sourceFilePath: sourceFilePath,
            sourceLineNumber: sourceLineNumber,
            timeout: timeout);

        return new([testCase]);
    }
}
