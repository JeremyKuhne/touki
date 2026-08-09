// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Collections;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Touki.Io;

/// <summary>
///  Enumerates files that match a glob pattern with zero allocations until matches are found.
/// </summary>
/// <remarks>
///  <para>The following wildcard patterns are supported:
///   <list type="table">
///    <listheader>
///     <term>Pattern</term>
///     <description>Description</description>
///    </listheader>
///    <item>
///     <term>*</term>
///     <description>Matches zero or more characters within a file or directory name</description>
///    </item>
///    <item>
///     <term>**</term>
///     <description>Matches zero or more directories (recursive wildcard)</description>
///    </item>
///    <item>
///     <term>?</term>
///     <description>Matches a single character</description>
///    </item>
///   </list>
///  </para>
/// </remarks>
public sealed class MSBuildEnumerator : FileSystemEnumerator<string>
{
    /// <summary>
    ///  Default options for the enumerator.
    /// </summary>
    private static EnumerationOptions DefaultOptions { get; } = new()
    {
        MatchType = MatchType.Simple,
        MatchCasing = MatchCasing.PlatformDefault,
        IgnoreInaccessible = true,
        RecurseSubdirectories = true
    };

    private readonly string _projectDirectory;
    private readonly bool _stripProjectDirectory;
    private readonly int _projectDirectoryLength;
    private readonly IReadOnlyList<string> _invalidExcludeSpecs;
    private readonly IFileSystemMatcherSession _session;
    private int _sessionDisposed;

    /// <summary>
    ///  Initializes a new instance of the <see cref="MSBuildEnumerator"/> class.
    /// </summary>
    private MSBuildEnumerator(
        IFileSystemMatcherSession matcher,
        string? projectDirectory,
        bool stripProjectDirectory,
        string startDirectory,
        EnumerationOptions options,
        IReadOnlyList<string>? invalidExcludeSpecs = null)
        : base(startDirectory, options)
    {
        _session = matcher;

        // Initialize project directory settings
        if (projectDirectory is null || !stripProjectDirectory)
        {
            _stripProjectDirectory = false;
            _projectDirectory = string.Empty;
            _projectDirectoryLength = 0;
        }
        else
        {
            _stripProjectDirectory = true;
            _projectDirectory = projectDirectory;
            _projectDirectoryLength = projectDirectory.Length +
                (Path.EndsInDirectorySeparator(_projectDirectory) ? 0 : 1);
        }

        _invalidExcludeSpecs = invalidExcludeSpecs ?? Array.Empty<string>();
    }

    /// <summary>
    ///  Creates a lazy enumerator for a request that resolves to a search.
    /// </summary>
    /// <exception cref="ArgumentException">The include must be returned literally.</exception>
    /// <exception cref="global::System.IO.DirectoryNotFoundException">
    ///  The resolved fixed start directory does not exist.
    /// </exception>
    /// <exception cref="InvalidOperationException">The request is rejected by a safety policy.</exception>
    public static MSBuildEnumerator Create(MSBuildEnumerationRequest request)
    {
        MSBuildEnumerationPlan plan = CreatePlan(request, returnEmptyForMissingStartDirectory: false);
        if (plan.Enumerator is { } enumerator)
        {
            return enumerator;
        }

        return plan.Result switch
        {
            MSBuildReturnLiteralResult literal => throw new ArgumentException(literal.Reason, nameof(request)),
            MSBuildEmptyResult => throw new global::System.IO.DirectoryNotFoundException(
                "The resolved MSBuild enumeration start directory does not exist."),
            MSBuildRejectedResult rejected => throw new InvalidOperationException(rejected.Message),
            _ => throw new InvalidOperationException("Unknown MSBuild enumeration result.")
        };
    }

    /// <summary>
    ///  Plans an MSBuild-compatible request as one closed result variant.
    /// </summary>
    /// <param name="request">The request to validate, parse, and plan.</param>
    /// <remarks>
    ///  <para>
    ///   The caller owns <see cref="MSBuildSearchResult.Enumerator"/> when a
    ///   <see cref="MSBuildSearchResult"/> is returned.
    ///  </para>
    /// </remarks>
    public static MSBuildEnumerationResult CreateResult(MSBuildEnumerationRequest request)
    {
        MSBuildEnumerationPlan plan = CreatePlan(request, returnEmptyForMissingStartDirectory: true);
        if (plan.Result is { } result)
        {
            return result;
        }

        MSBuildEnumerator enumerator = plan.Enumerator
            ?? throw new InvalidOperationException("The MSBuild enumeration plan is invalid.");
        try
        {
            return new MSBuildSearchResult(enumerator, plan.InvalidExcludeSpecifications);
        }
        catch
        {
            enumerator.Dispose();
            throw;
        }
    }

    private static MSBuildEnumerationPlan CreatePlan(
        MSBuildEnumerationRequest request,
        bool returnEmptyForMissingStartDirectory)
    {
        string fileSpec = request.Include
            ?? throw new ArgumentException("The request must be initialized with an include specification.", nameof(request));
        ArgumentNullException.ThrowIfNull(fileSpec);

        EnumerationOptions enumOptions = request.EnumerationOptions is { } suppliedEnumerationOptions
            ? SnapshotEnumerationOptions(suppliedEnumerationOptions)
            : DefaultOptions;
        string? excludeSpecs = request.Excludes;
        string? projectDirectory = request.ProjectDirectory is null
            ? null
            : FileSystemMatchEnumeratorArguments.NormalizeRootDirectory(request.ProjectDirectory);
        string rootDirectory = projectDirectory
            ?? FileSystemMatchEnumeratorArguments.NormalizeRootDirectory(Environment.CurrentDirectory);

        // Validate the include spec against MSBuild's "legal file spec" rules before we ever try to
        // build an MSBuildSpecification. When the spec is illegal, MSBuild's FileMatcher.GetFiles
        // returns it verbatim via SearchAction.ReturnFileSpec; we mirror that with an
        // MSBuildReturnLiteralResult carrying the validation reason.
        StringSegment fileSpecSegment = new(fileSpec);
        StringSegment normalizedFileSpec = MSBuildSpecification.NormalizeAndValidate(fileSpecSegment, out string? includeError);

        if (includeError is not null)
        {
            return new(new MSBuildReturnLiteralResult(
                fileSpec,
                $"Specification '{fileSpec}' is not a legal file spec: {includeError}"));
        }

        // Parse once and drive the match builder directly with the already-qualified include.
        MSBuildSpecification include = new MSBuildSpecification(fileSpecSegment, normalizedFileSpec).FullyQualify(rootDirectory);

        if (!request.AllowDriveEnumeration && include.IsDriveRootRecursion)
        {
            return new(new MSBuildRejectedResult(
                MSBuildRejectionReason.DriveEnumerationForbidden,
                $"Drive enumeration is not allowed for '{fileSpec}'. Set " +
                    $"{nameof(MSBuildEnumerationRequest)}.{nameof(MSBuildEnumerationRequest.AllowDriveEnumeration)} to true to override."));
        }

        bool ignoreCase = Paths.GetFinalCasing(enumOptions.MatchCasing) == MatchCasing.CaseInsensitive;
        ListBase<MSBuildSpecificationResult>? excludeResults = null;
        SingleOptimizedList<MSBuildSpecification, ArrayPoolList<MSBuildSpecification>>? parsedExcludes = null;
        ListBase<MSBuildSpecification> excludes = EmptyList<MSBuildSpecification>.Instance;
        string[] invalidExcludeSpecs = [];

        if (!string.IsNullOrEmpty(excludeSpecs))
        {
            excludeResults = MSBuildSpecification.SplitWithErrors(excludeSpecs!, ignoreCase);
            parsedExcludes = [];
            SingleOptimizedList<string, ArrayPoolList<string>> ignoredExcludes = [];
            try
            {
                for (int i = 0; i < excludeResults.Count; i++)
                {
                    MSBuildSpecificationResult result = excludeResults[i];
                    if (result.IsError)
                    {
                        ignoredExcludes.Add(result.Original.ToString());
                    }
                    else
                    {
                        parsedExcludes.Add(result.Specification);
                    }
                }

                if (ignoredExcludes.Count > 0)
                {
                    invalidExcludeSpecs = new string[ignoredExcludes.Count];
                    ignoredExcludes.CopyTo(invalidExcludeSpecs, 0);
                }
            }
            finally
            {
                ignoredExcludes.Dispose();
            }

            excludes = parsedExcludes;
        }

        try
        {
            MSBuildMatchBuildResult buildResult = MSBuildMatchBuilder.FromSpecification(
                include,
                excludes,
                enumOptions.MatchType,
                enumOptions.MatchCasing,
                rootDirectory);
            IFileSystemMatcherSession matcher = buildResult.Session;

            string startDirectoryString = buildResult.StartDirectory.ToString();

            // Mirror MSBuild's FileMatcher.GetFileSearchData: when the resolved fixed directory does
            // not exist as a directory on disk, return an empty list rather than letting the
            // underlying FileSystemEnumerator throw on first iteration. This catches both trailing-
            // separator specs that resolve onto a file (e.g. "Foo/b.txt/") and specs naming a missing
            // subdirectory (e.g. "Missing/", "Missing/**").
            if (returnEmptyForMissingStartDirectory && !Directory.Exists(startDirectoryString))
            {
                matcher.Dispose();
                return new(new MSBuildEmptyResult(MSBuildEmptyReason.StartDirectoryNotFound));
            }

            MSBuildEnumerator enumerator;
            try
            {
                enumerator = new(
                    matcher,
                    projectDirectory,
                    stripProjectDirectory: !Path.IsPathFullyQualified(fileSpec),
                    startDirectoryString,
                    enumOptions,
                    invalidExcludeSpecs);
            }
            catch
            {
                matcher.Dispose();
                throw;
            }

            return new(enumerator, invalidExcludeSpecs);
        }
        finally
        {
            parsedExcludes?.Dispose();
            excludeResults?.Dispose();
        }
    }

    private static EnumerationOptions SnapshotEnumerationOptions(EnumerationOptions options) => new()
    {
        AttributesToSkip = options.AttributesToSkip,
        BufferSize = options.BufferSize,
        IgnoreInaccessible = options.IgnoreInaccessible,
        MatchCasing = options.MatchCasing,
        MatchType = options.MatchType,
        MaxRecursionDepth = options.MaxRecursionDepth,
        RecurseSubdirectories = options.RecurseSubdirectories,
        ReturnSpecialDirectories = options.ReturnSpecialDirectories
    };

    /// <inheritdoc/>
    protected override bool ShouldIncludeEntry(ref FileSystemEntry entry) =>
        !entry.IsDirectory
        && _session.MatchesFile(entry.Directory, entry.FileName)
        && ShouldIncludeMatchedFile(ref entry);

    /// <inheritdoc/>
    protected override bool ShouldRecurseIntoEntry(ref FileSystemEntry entry) =>
        _session.MatchesDirectory(entry.Directory, entry.FileName)
            != DirectoryMatchType.NoDescendantFilesMatch;

    /// <inheritdoc/>
    protected override void OnDirectoryFinished(ReadOnlySpan<char> directory) =>
        _session.DirectoryFinished(directory);

    private bool ShouldIncludeMatchedFile(ref FileSystemEntry entry)
    {
        for (int i = 0; i < _invalidExcludeSpecs.Count; i++)
        {
            if (MatchesResultPath(ref entry, _invalidExcludeSpecs[i]))
            {
                return false;
            }
        }

        return true;
    }

    private bool MatchesResultPath(ref FileSystemEntry entry, ReadOnlySpan<char> exclude)
    {
        if (!Path.IsPathFullyQualified(exclude) && !_stripProjectDirectory)
        {
            return false;
        }

        if (_stripProjectDirectory && !EntryDirectoryIsWithinProjectDirectory(entry.Directory))
        {
            string relativePath = Path.GetRelativePath(_projectDirectory, entry.ToFullPath());
            return exclude.SequenceEqual(relativePath);
        }

        ReadOnlySpan<char> prefix;
        if (!_stripProjectDirectory)
        {
            prefix = entry.Directory;
        }
        else if (entry.Directory.Length <= _projectDirectoryLength)
        {
            return exclude.SequenceEqual(entry.FileName);
        }
        else
        {
            prefix = entry.Directory[_projectDirectoryLength..];
        }

        return MatchesResultPath(prefix, entry.FileName, exclude);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        Exception? firstException = null;
        try
        {
            base.Dispose(disposing);
        }
        catch (Exception exception)
        {
            firstException = exception;
        }

        if (disposing && Interlocked.Exchange(ref _sessionDisposed, 1) == 0)
        {
            try
            {
                _session.Dispose();
            }
            catch (Exception exception)
            {
                firstException ??= exception;
            }
        }

        if (firstException is not null)
        {
            ExceptionDispatchInfo.Capture(firstException).Throw();
        }
    }

    internal static bool MatchesResultPath(
        ReadOnlySpan<char> prefix,
        ReadOnlySpan<char> fileName,
        ReadOnlySpan<char> exclude)
    {
        bool needsSeparator = prefix.IsEmpty || prefix[^1] != Path.DirectorySeparatorChar;
        int separatorLength = needsSeparator ? 1 : 0;
        return exclude.Length == prefix.Length + separatorLength + fileName.Length
            && exclude.StartsWith(prefix, StringComparison.Ordinal)
            && (!needsSeparator || exclude[prefix.Length] == Path.DirectorySeparatorChar)
            && exclude[(prefix.Length + separatorLength)..].SequenceEqual(fileName);
    }

    /// <inheritdoc/>
    protected override string TransformEntry(ref FileSystemEntry entry)
    {
        if (!_stripProjectDirectory)
        {
            // If we're not stripping the project directory, we can just return the full path.
            return entry.ToFullPath();
        }

        if (!EntryDirectoryIsWithinProjectDirectory(entry.Directory))
        {
            return Path.GetRelativePath(_projectDirectory, entry.ToFullPath());
        }

        if (entry.Directory.Length <= _projectDirectoryLength)
        {
            // If the entry is in the base directory, we can just return the file name.
            return entry.FileName.ToString();
        }

        return $"{entry.Directory[_projectDirectoryLength..]}{Path.DirectorySeparatorChar}{entry.FileName}";
    }

    private bool EntryDirectoryIsWithinProjectDirectory(ReadOnlySpan<char> entryDirectory) =>
        Paths.IsSameOrSubdirectory(
            _projectDirectory,
            entryDirectory,
            ignoreCase: Paths.GetFinalCasing(MatchCasing.PlatformDefault) == MatchCasing.CaseInsensitive);
}
