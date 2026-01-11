// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Touki.TestSupport;

/// <summary>
///  A <see cref="TraceListener"/> that throws exceptions for failed assertions.
/// </summary>
/// <remarks>
///  <para>
///   Intended for test scenarios where trace assertions should fail the test immediately.
///  </para>
/// </remarks>
public sealed class ThrowingTraceListener : TraceListener
{
    /// <summary>
    ///  Gets a shared instance of the listener.
    /// </summary>
    public static ThrowingTraceListener Instance { get; } = new();

    /// <inheritdoc/>
    public override void Fail(string? message, string? detailMessage)
    {
        throw new InvalidOperationException(
            $"{(string.IsNullOrEmpty(message) ? "Assertion failed" : message)}{(string.IsNullOrEmpty(detailMessage)
                ? ""
                : $"{Environment.NewLine}{detailMessage}")}");
    }

    /// <inheritdoc/>
    public override void Write(object? o)
    {
    }

    /// <inheritdoc/>
    public override void Write(object? o, string? category)
    {
    }

    /// <inheritdoc/>
    public override void Write(string? message)
    {
    }

    /// <inheritdoc/>
    public override void Write(string? message, string? category)
    {
    }

    /// <inheritdoc/>
    public override void WriteLine(object? o)
    {
    }

    /// <inheritdoc/>
    public override void WriteLine(object? o, string? category)
    {
    }

    /// <inheritdoc/>
    public override void WriteLine(string? message)
    {
    }

    /// <inheritdoc/>
    public override void WriteLine(string? message, string? category)
    {
    }
}
