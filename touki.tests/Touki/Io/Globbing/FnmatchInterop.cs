// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET

namespace Touki.Io.Globbing;

/// <summary>
///  Thin wrapper around POSIX <c>fnmatch(3)</c> via P/Invoke for use as an oracle by the
///  per-dialect oracle test classes. Linux and macOS use different bit values for the
///  <c>FNM_*</c> flags — this class encapsulates the platform shim.
/// </summary>
internal static class FnmatchInterop
{
    public static bool IsSupported => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

    // glibc / musl: FNM_PATHNAME = 0x01
    // macOS:        FNM_PATHNAME = 0x02
    public static int FnmPathname => OperatingSystem.IsMacOS() ? 0x02 : 0x01;

    [DllImport("libc", EntryPoint = "fnmatch", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern int fnmatch(
        [MarshalAs(UnmanagedType.LPStr)] string pattern,
        [MarshalAs(UnmanagedType.LPStr)] string str,
        int flags);

    /// <summary>
    ///  Returns <see langword="true"/> when <paramref name="pattern"/> matches
    ///  <paramref name="input"/> per <c>fnmatch(3)</c> with the given flags.
    /// </summary>
    public static bool Matches(string pattern, string input, int flags) =>
        fnmatch(pattern, input, flags) == 0;
}

#endif
