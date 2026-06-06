// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Diagnostics.CodeAnalysis;

namespace TraceQ.Tracing;

/// <summary>
///  The symbol-resolution quality gate: the single policy deciding when a trace's
///  frames are too poorly resolved to trust managed-method rankings, and the
///  standardized warning - with remediation - that surfaces it.
/// </summary>
/// <remarks>
///  <para>
///   Centralizing the threshold keeps the readers that compute a resolution rate,
///   the output contract that warns on it, and the future CLI <c>--strict</c> gate
///   all agreeing on one number and one message. The <c>--strict</c> -> exit-code
///   behavior is a CLI concern (it lands with the M2 head) and consumes
///   <see cref="IsBelowThreshold"/>.
///  </para>
/// </remarks>
internal static class SymbolGate
{
    /// <summary>
    ///  The minimum fraction of frames, in <c>[0, 1]</c>, that must resolve to a
    ///  method name before rankings are trusted. Resolution below this fires the gate.
    /// </summary>
    public const double MinimumResolutionRate = 0.8;

    /// <summary>
    ///  Determines whether a trace's symbol resolution is below
    ///  <see cref="MinimumResolutionRate"/>.
    /// </summary>
    /// <param name="resolutionRate">The fraction in <c>[0, 1]</c> of frames that resolved.</param>
    /// <param name="sampleCount">
    ///  The number of samples. Zero suppresses the gate: a trace with no samples has
    ///  nothing to resolve, and a separate warning already covers that case.
    /// </param>
    /// <returns><see langword="true"/> when resolution is below the threshold.</returns>
    public static bool IsBelowThreshold(double resolutionRate, int sampleCount) =>
        sampleCount > 0 && resolutionRate < MinimumResolutionRate;

    /// <summary>
    ///  Produces the standardized low-resolution warning, with remediation, when the
    ///  gate fires for the given rate.
    /// </summary>
    /// <param name="resolutionRate">The fraction in <c>[0, 1]</c> of frames that resolved.</param>
    /// <param name="sampleCount">The number of samples.</param>
    /// <param name="warning">The warning text when the gate fires, otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a warning was produced.</returns>
    public static bool TryGetWarning(double resolutionRate, int sampleCount, [NotNullWhen(true)] out string? warning)
    {
        if (!IsBelowThreshold(resolutionRate, sampleCount))
        {
            warning = null;
            return false;
        }

        // Format whole-number percentages so the warning text is deterministic and
        // free of the locale-dependent space the "P" format inserts before "%".
        int resolvedPct = (int)Math.Round(resolutionRate * 100, MidpointRounding.AwayFromZero);
        int thresholdPct = (int)Math.Round(MinimumResolutionRate * 100, MidpointRounding.AwayFromZero);
        warning =
            $"Only {resolvedPct}% of frames resolved to a method name (< {thresholdPct}%); native frames may be unresolved. "
            + "Managed frames resolve from CLR rundown, so managed-method rankings remain usable. "
            + "Pass --symbols <build-output-dir> to resolve more frames from local PDBs.";
        return true;
    }
}
