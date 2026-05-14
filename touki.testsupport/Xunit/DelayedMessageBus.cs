// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from the xUnit.net v3 RetryFactExample sample.
// https://github.com/xunit/samples.xunit/tree/main/v3/RetryFactExample

using Xunit.Sdk;
using Xunit.v3;

namespace Xunit;

/// <summary>
///  Message bus that buffers messages until disposal. Used by
///  <see cref="RetryTestCaseRunner"/> to delay the publication of per-attempt
///  status messages until the final attempt's verdict is known.
/// </summary>
public class DelayedMessageBus(IMessageBus innerBus) : IMessageBus
{
    private readonly List<IMessageSinkMessage> _messages = [];

    /// <inheritdoc/>
    public bool QueueMessage(IMessageSinkMessage message)
    {
        // The lock is technically unnecessary because each retry uses a fresh bus
        // and the runner does not produce parallel messages, but it is kept as
        // defense-in-depth for future callers that may share an instance.
        lock (_messages)
        {
            _messages.Add(message);
        }

        // There is no way to ask the inner bus if it wants to cancel without first
        // forwarding the message, so always continue.
        return true;
    }

    /// <summary>
    ///  Flushes all buffered messages to the inner bus.
    /// </summary>
    public void Dispose()
    {
        foreach (IMessageSinkMessage message in _messages)
        {
            innerBus.QueueMessage(message);
        }

        GC.SuppressFinalize(this);
    }
}
