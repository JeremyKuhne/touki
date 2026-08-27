// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

internal sealed partial class MatchMSBuild
{
    /// <summary>
    ///  Stores one parsed directory specification segment together with globstar and case-matching metadata.
    /// </summary>
    private readonly struct SpecSegment
    {
        public StringSegment Spec { get; }
        public bool IsAnyDirectory { get; }
        public bool IgnoreCase { get; }

        /// <summary>
        ///  Implicitly converts a <see cref="SpecSegment"/> to a <see cref="ReadOnlySpan{T}"/> of <see cref="char"/>.
        /// </summary>
        /// <param name="segment">The segment to convert.</param>
        public static implicit operator ReadOnlySpan<char>(SpecSegment segment) => segment.Spec;

        public SpecSegment(StringSegment spec, bool ignoreCase)
        {
            Spec = spec;
            IsAnyDirectory = spec.Equals("**");
            IgnoreCase = ignoreCase;
        }

        public override string ToString() => Spec.ToString();
    }
}
