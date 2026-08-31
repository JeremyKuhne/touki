// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

public sealed partial class RequireNamedArgumentsForLiteralsAnalyzer
{
    private sealed class ConfiguredLiteralKinds
    {
        private readonly object _sync = new();
        private LiteralKinds _kinds;
        private bool _initialized;

        public LiteralKinds Get(
            AnalyzerConfigOptionsProvider optionsProvider,
            SyntaxTree tree,
            CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref _initialized))
            {
                return _kinds;
            }

            lock (_sync)
            {
                if (_initialized)
                {
                    return _kinds;
                }

                cancellationToken.ThrowIfCancellationRequested();
                AnalyzerConfigOptions options = optionsProvider.GetOptions(tree);
                _kinds = options.TryGetValue(LiteralsOption, out string? configured)
                    && TryParseLiteralKinds(configured, cancellationToken, out LiteralKinds parsedKinds)
                        ? parsedKinds
                        : DefaultLiteralKinds;
                Volatile.Write(ref _initialized, true);
                return _kinds;
            }
        }
    }
}