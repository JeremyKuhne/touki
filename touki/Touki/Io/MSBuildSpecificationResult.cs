// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Result of classifying a single MSBuild specification produced by
///  <see cref="MSBuildSpecification.SplitWithErrors(StringSegment, bool)"/>.
/// </summary>
/// <remarks>
///  <para>
///   A result is either a successfully parsed <see cref="MSBuildSpecification"/> or an "error" spec
///   that MSBuild's <c>FileMatcher</c> would surface as a literal string rather than evaluating as a
///   glob. Use <see cref="IsError"/> to discriminate; on success read <see cref="Specification"/>,
///   on failure read <see cref="Original"/> and <see cref="ErrorReason"/>.
///  </para>
/// </remarks>
public readonly struct MSBuildSpecificationResult
{
    /// <summary>
    ///  The raw specification as it appeared in the input, before normalization.
    /// </summary>
    public StringSegment Original { get; }

    /// <summary>
    ///  The parsed specification when <see cref="IsError"/> is <see langword="false"/>; otherwise <see langword="null"/>.
    /// </summary>
    public MSBuildSpecification? Specification { get; }

    /// <summary>
    ///  A short human-readable reason describing why the spec was classified as an error, or
    ///  <see langword="null"/> when <see cref="IsError"/> is <see langword="false"/>.
    /// </summary>
    public string? ErrorReason { get; }

    /// <summary>
    ///  <see langword="true"/> when this result represents an error spec that should be surfaced as a literal
    ///  string rather than evaluated as a glob.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Specification))]
    [MemberNotNullWhen(true, nameof(ErrorReason))]
    public bool IsError => Specification is null;

    private MSBuildSpecificationResult(StringSegment original, MSBuildSpecification? specification, string? errorReason)
    {
        Original = original;
        Specification = specification;
        ErrorReason = errorReason;
    }

    /// <summary>
    ///  Creates a successful result wrapping the given <paramref name="specification"/>.
    /// </summary>
    public static MSBuildSpecificationResult FromSpecification(MSBuildSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        return new MSBuildSpecificationResult(specification.Original, specification, errorReason: null);
    }

    /// <summary>
    ///  Creates an error result for the given raw <paramref name="original"/> spec.
    /// </summary>
    /// <param name="original">The raw specification as it appeared in the input.</param>
    /// <param name="errorReason">A short human-readable reason describing the failure.</param>
    public static MSBuildSpecificationResult FromError(StringSegment original, string errorReason)
    {
        ArgumentNullException.ThrowIfNull(errorReason);
        return new MSBuildSpecificationResult(original, specification: null, errorReason);
    }
}
