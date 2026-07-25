// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

public sealed partial class OrderedMatchSet
{
    private readonly struct Rule
    {
        public Rule(IEnumerationMatcher matcher, bool isExclude)
        {
            Matcher = matcher;
            IsExclude = isExclude;
        }

        public IEnumerationMatcher Matcher { get; }

        public bool IsExclude { get; }
    }
}
