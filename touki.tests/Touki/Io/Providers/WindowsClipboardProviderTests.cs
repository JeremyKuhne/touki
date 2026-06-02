// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;
using Windows.Win32.System.Ole;

namespace Touki.Io.Providers;

/// <summary>
///  Windows-specific tests that exercise <see cref="WindowsClipboardProvider"/>
///  paths that cannot be reached through the cross-platform <see cref="Clipboard"/>
///  surface. The CsWin32-generated <c>Windows.Win32.PInvoke</c> surface lives in
///  the touki assembly as <c>internal</c>; this test class reaches it through the
///  <c>InternalsVisibleTo</c> grant.
/// </summary>
/// <remarks>
///  <para>
///   The class shares <see cref="ClipboardTests"/>' sequential <c>NotInParallel</c> group
///   so the clipboard is not touched by parallel tests. Tests skip at runtime on
///   non-Windows hosts because the .NET 10 build also runs on Linux and macOS.
///  </para>
/// </remarks>
[NotInParallel("Sequential")]
[SupportedOSPlatform("windows5.1.2600")]
public unsafe class WindowsClipboardProviderTests
{
    [Test, Retry(4), SkipUnlessClipboardAvailable]
    public void TryGetText_WhenClipboardHasZeroByteUnicodeTextHandle_ExercisesDefensivePath()
    {
#if NET
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("Exercises Win32 PInvoke surface; Windows-only test.");
        }
#endif
        string original = SnapshotText();
        try
        {
            // Set a CF_UNICODETEXT entry backed by a moveable 0-byte HGLOBAL. The OS
            // takes ownership when SetClipboardData succeeds, so the test must not
            // GlobalFree on the success path.
            bool opened = PInvoke.OpenClipboard(HWND.Null);
            opened.Should().BeTrue();
            try
            {
                bool emptied = PInvoke.EmptyClipboard();
                emptied.Should().BeTrue();
                HGLOBAL hGlobal = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, 0);
                hGlobal.IsNull.Should().BeFalse();
                HANDLE result = PInvoke.SetClipboardData((uint)CLIPBOARD_FORMAT.CF_UNICODETEXT, (HANDLE)(void*)hGlobal);
                if (result.IsNull)
                {
                    // Some Windows hosts (notably the Windows Server SKUs used by GitHub
                    // Actions runners) refuse SetClipboardData(CF_UNICODETEXT) with a
                    // zero-byte HGLOBAL. That is a property of the host's clipboard
                    // subsystem, not a transient race, so a retry will not recover.
                    // Free the orphan handle, then skip with a clear reason instead of
                    // silently returning success or hard-failing.
                    PInvoke.GlobalFree(hGlobal);
                    Skip.Test("Host rejected SetClipboardData(CF_UNICODETEXT) with a zero-byte HGLOBAL; "
                        + "cannot exercise the zero-byte defensive path on this configuration.");
                }
            }
            finally
            {
                PInvoke.CloseClipboard();
            }

            // The provider walks: IsClipboardFormatAvailable (true) -> OpenClipboard ->
            // GetClipboardData -> GlobalLock on the 0-byte handle. Two outcomes are
            // valid on Windows:
            //   - GlobalLock returns NULL: TryGetText returns false (defensive
            //     pointer-null branch covered).
            //   - GlobalLock returns a valid pointer with GlobalSize == 0:
            //     TryGetText returns true with an empty string (empty-string
            //     fast-path branch covered).
            bool success = Clipboard.TryGetText(out string? text);
            if (success)
            {
                text.Should().Be(string.Empty);
            }
            else
            {
                text.Should().BeNull();
            }
        }
        finally
        {
            RestoreText(original);
        }
    }

    private static string SnapshotText()
    {
        if (Clipboard.TryGetText(out string? captured))
        {
            return captured;
        }

        return string.Empty;
    }

    private static void RestoreText(string text)
    {
        if (text.Length == 0)
        {
            Clipboard.TryClear();
        }
        else
        {
            Clipboard.TrySetText(text);
        }
    }
}
