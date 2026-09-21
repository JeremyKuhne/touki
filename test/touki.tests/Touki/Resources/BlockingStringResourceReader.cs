// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

/// <summary>
///  Blocks indexed lookup until released and records disposal.
/// </summary>
internal sealed class BlockingStringResourceReader : IStringResourceReader
{
    private readonly ManualResetEventSlim _lookupStarted;
    private readonly ManualResetEventSlim _continueLookup;
    private readonly ManualResetEventSlim _disposed;

    internal BlockingStringResourceReader(
        ManualResetEventSlim lookupStarted,
        ManualResetEventSlim continueLookup,
        ManualResetEventSlim disposed)
    {
        _lookupStarted = lookupStarted;
        _continueLookup = continueLookup;
        _disposed = disposed;
    }

    /// <inheritdoc/>
    public int ResourceCount => 1;

    /// <inheritdoc/>
    public ResourceTypeCode GetResourceTypeCode(int index) => ResourceTypeCode.String;

    /// <inheritdoc/>
    public string GetResourceName(int index) => "Greeting";

    /// <inheritdoc/>
    public string GetString(int index) => "Hello";

    /// <inheritdoc/>
    public StringResourceLookupKind Lookup(string name, out string? value)
    {
        _lookupStarted.Set();
        _continueLookup.Wait();
        value = "Hello";
        return StringResourceLookupKind.Found;
    }

    /// <inheritdoc/>
    public void Dispose() => _disposed.Set();
}
