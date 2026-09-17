// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Wraps source text and counts character reads after parsing has completed.
/// </summary>
internal sealed class CountingSourceText(SourceText inner) : SourceText
{
    private long _characterReads;

    public long CharacterReads => Interlocked.Read(ref _characterReads);

    public override Encoding? Encoding => inner.Encoding;

    public override int Length => inner.Length;

    public override char this[int position]
    {
        get
        {
            Interlocked.Increment(ref _characterReads);
            return inner[position];
        }
    }

    public override void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count)
    {
        Interlocked.Add(ref _characterReads, count);
        inner.CopyTo(sourceIndex, destination, destinationIndex, count);
    }

    public void Reset() => Interlocked.Exchange(ref _characterReads, 0);
}
