// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

public abstract partial class MSBuildEnumerator
{
    /// <summary>
    ///  Enumerator for the simplest scenario where a single <see cref="MSBuildSpec"/> is used.
    /// </summary>
    private sealed class SingleSpec : MSBuildEnumerator
    {
        private readonly MSBuildSpec _spec;

        /// <summary>
        ///  Initializes a new instance of the <see cref="SingleSpec"/> class.
        /// </summary>
        public SingleSpec(
            MSBuildSpec includeSpec,
            string? projectDirectory,
            bool stripProjectDirectory,
            string startDirectory,
            EnumerationOptions options)
            : base(projectDirectory, stripProjectDirectory, startDirectory, options)
        {
            _spec = includeSpec;
        }

        /// <inheritdoc/>
        protected override void OnDirectoryFinished(ReadOnlySpan<char> directory) =>
            // Clear the cache when we finish processing a directory
            _spec.InvalidateCache();

        /// <inheritdoc/>
        protected override bool ShouldRecurseIntoEntry(ref FileSystemEntry entry) =>
            _spec.ShouldRecurseIntoDirectory(entry.Directory, entry.FileName);

        /// <inheritdoc/>
        protected override bool ShouldIncludeEntry(ref FileSystemEntry entry) =>
            !entry.IsDirectory && _spec.ShouldIncludeFile(entry.Directory, entry.FileName);

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _spec.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
