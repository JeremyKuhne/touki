// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Providers;

namespace Touki.Io;

/// <summary>
///  Portable plain-text clipboard access for Windows, macOS, and Linux.
/// </summary>
/// <remarks>
///  <para>
///   All operations return <see langword="false"/> rather than throwing when the underlying
///   clipboard transport is unavailable. The retention model varies by platform:
///  </para>
///  <para>
///   On Windows the clipboard data is owned by the operating system. Text set via
///   <see cref="TrySetText(ReadOnlySpan{char})"/> persists until another application overwrites
///   it, the clipboard is cleared, or the system is restarted.
///  </para>
///  <para>
///   On macOS the clipboard is hosted by the per-user <c>pasteboardd</c> daemon. Text persists
///   under the same conditions as Windows. The provider calls <c>NSPasteboard</c> directly via
///   the Objective-C runtime; no <c>pbcopy</c>/<c>pbpaste</c> process is spawned.
///  </para>
///  <para>
///   On Linux the clipboard is owned by a client process, not the operating system. The provider
///   delegates to <c>wl-copy</c>/<c>wl-paste</c> on Wayland or <c>xclip</c>/<c>xsel</c> on X11,
///   which fork themselves into the background to hold the selection so the text outlives the
///   calling process. Text may still be lost if no clipboard manager is running and the helper
///   process is terminated. On a headless or unsupported Linux system, all operations return
///   <see langword="false"/>.
///  </para>
///  <para>
///   On .NET Framework only the Windows transport is compiled in; the runtime does not run on
///   any other platform.
///  </para>
/// </remarks>
public static class Clipboard
{
    private static readonly IClipboardProvider s_provider = SelectProvider();

    /// <summary>
    ///  Returns <see langword="true"/> if a clipboard transport is reachable on the current platform.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This is a static property of the platform and host environment, not of any individual
    ///   call. It returns <see langword="true"/> on Windows and macOS; on Linux it returns
    ///   <see langword="true"/> only if a supported helper (<c>wl-copy</c>/<c>wl-paste</c>,
    ///   <c>xclip</c>, or <c>xsel</c>) is on <c>PATH</c> and the associated display server is
    ///   running. On any other platform or a headless Linux host it returns <see langword="false"/>.
    ///  </para>
    ///  <para>
    ///   Even when <see cref="IsAvailable"/> is <see langword="true"/>, individual operations can
    ///   still fail because the clipboard is a system-wide resource and other processes can
    ///   briefly own it.
    ///  </para>
    /// </remarks>
    public static bool IsAvailable => s_provider.IsAvailable;

    /// <summary>
    ///  Returns <see langword="true"/> if Unicode text is currently available on the clipboard.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Probing the clipboard on Linux is significantly more expensive than on Windows or
    ///   macOS because it round-trips through the helper process. Avoid polling this property
    ///   in a tight loop.
    ///  </para>
    /// </remarks>
    public static bool HasText => s_provider.HasText;

    /// <summary>
    ///  Attempts to read Unicode text from the clipboard.
    /// </summary>
    /// <param name="text">
    ///  When this method returns <see langword="true"/>, contains the clipboard text.
    ///  When <see langword="false"/>, contains <see langword="null"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if text was successfully read; otherwise <see langword="false"/>.
    /// </returns>
    public static bool TryGetText([NotNullWhen(true)] out string? text) => s_provider.TryGetText(out text);

    /// <summary>
    ///  Attempts to place <paramref name="text"/> on the clipboard as Unicode text.
    /// </summary>
    /// <param name="text">The text to copy.</param>
    /// <returns>
    ///  <see langword="true"/> if the text was successfully placed on the clipboard; otherwise
    ///  <see langword="false"/>.
    /// </returns>
    public static bool TrySetText(ReadOnlySpan<char> text) => s_provider.TrySetText(text);

    /// <summary>
    ///  Attempts to release the current clipboard contents.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   On Linux this stops the helper process from offering our previously-set value; the
    ///   current clipboard owner (a clipboard manager, or another application) is not affected.
    ///  </para>
    /// </remarks>
    public static bool TryClear() => s_provider.TryClear();

    // Platform dispatch is structurally untestable: any single CI runner can
    // only exercise its own OS branch. The Mac/Linux/Null fallbacks are
    // unreachable on the Windows runner that produces our coverage data.
    [ExcludeFromCodeCoverage]
    private static IClipboardProvider SelectProvider()
    {
#if NET
        // The Win32 clipboard / GlobalAlloc APIs are documented as available since
        // Windows XP SP1 (5.1.2600). Modern .NET only runs on far newer Windows, but
        // the explicit minimum keeps the CA1416 analyzer happy without per-call suppressions.
        if (OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
        {
            return WindowsClipboardProvider.Instance;
        }

        if (OperatingSystem.IsMacOS())
        {
            return MacClipboardProvider.Instance;
        }

        if (OperatingSystem.IsLinux())
        {
            return LinuxClipboardProvider.Instance;
        }

        return NullClipboardProvider.Instance;
#else
        return WindowsClipboardProvider.Instance;
#endif
    }
}
