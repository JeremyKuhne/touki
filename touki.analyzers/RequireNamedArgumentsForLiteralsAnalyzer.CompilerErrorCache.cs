// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

public sealed partial class RequireNamedArgumentsForLiteralsAnalyzer
{
    private sealed class CompilerErrorCache
    {
        private readonly object _sync = new();
        private ImmutableArray<TextSpan> _spans = [];
        private bool _initialized;

        public bool Overlaps(SemanticModel semanticModel, TextSpan candidate, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureInitialized(semanticModel, cancellationToken);

            int low = 0;
            int high = _spans.Length;
            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                if (_spans[middle].End <= candidate.Start)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return low < _spans.Length && _spans[low].OverlapsWith(candidate);
        }

        private void EnsureInitialized(SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                if (_initialized)
                {
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                List<TextSpan> spans = [];
                foreach (Diagnostic diagnostic in semanticModel.GetDiagnostics(cancellationToken: cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (diagnostic.Severity == DiagnosticSeverity.Error
                        && diagnostic.Location.IsInSource
                        && ReferenceEquals(diagnostic.Location.SourceTree, semanticModel.SyntaxTree))
                    {
                        spans.Add(diagnostic.Location.SourceSpan);
                    }
                }

                Comparison<TextSpan> comparison = (left, right) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return left.Start.CompareTo(right.Start);
                };
                try
                {
                    spans.Sort(comparison);
                }
                catch (InvalidOperationException exception)
                    when (exception.InnerException is OperationCanceledException
                        && cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw;
                }

                ImmutableArray<TextSpan>.Builder merged = ImmutableArray.CreateBuilder<TextSpan>(spans.Count);
                if (spans.Count > 0)
                {
                    TextSpan current = spans[0];
                    for (int i = 1; i < spans.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        TextSpan next = spans[i];
                        if (next.Start <= current.End)
                        {
                            current = TextSpan.FromBounds(current.Start, Math.Max(current.End, next.End));
                        }
                        else
                        {
                            merged.Add(current);
                            current = next;
                        }
                    }

                    merged.Add(current);
                }

                _spans = merged.ToImmutable();
                _initialized = true;
            }
        }
    }
}