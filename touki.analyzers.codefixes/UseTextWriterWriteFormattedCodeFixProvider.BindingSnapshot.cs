// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

public sealed partial class UseTextWriterWriteFormattedCodeFixProvider
{
    /// <summary>
    ///  Records the symbol identity validated before rewriting.
    /// </summary>
    private sealed class BindingSnapshot(string symbolIdentity)
    {
        public string SymbolIdentity { get; } = symbolIdentity;
    }
}