// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

public abstract partial class MSBuildEnumerator
{
    /// <summary>
    ///  Enumerator for the simplest scenario where a single <see cref="MatchMSBuild"/> is used.
    /// </summary>
    private sealed class SingleSpec : MSBuildEnumerator
    {
        private readonly IEnumerationMatcher _matcher;

        /// <summary>
        ///  Initializes a new instance of the <see cref="SingleSpec"/> class.
        /// </summary>
        public SingleSpec(
            IEnumerationMatcher includeMatcher,
            string? projectDirectory,
            bool stripProjectDirectory,
            string startDirectory,
            EnumerationOptions options)
            : base(projectDirectory, stripProjectDirectory, startDirectory, options)
        {
            _matcher = includeMatcher;
        }

        /// <inheritdoc/>
        protected override void OnDirectoryFinished(ReadOnlySpan<char> directory) =>
            // Clear the cache when we finish processing a directory
            _matcher.DirectoryFinished();

        /// <inheritdoc/>
        protected override bool ShouldRecurseIntoEntry(ref FileSystemEntry entry) =>
            _matcher.MatchesDirectory(entry.Directory, entry.FileName);

        /// <inheritdoc/>
        protected override bool ShouldIncludeEntry(ref FileSystemEntry entry) =>
            !entry.IsDirectory && _matcher.MatchesFile(entry.Directory, entry.FileName);

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _matcher.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
