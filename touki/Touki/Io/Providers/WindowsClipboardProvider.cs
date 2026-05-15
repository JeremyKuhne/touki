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
///  Windows clipboard transport for <see cref="Clipboard"/>.
/// </summary>
/// <remarks>
///  <para>
///   Uses the classic Win32 clipboard APIs (<c>OpenClipboard</c>, <c>SetClipboardData</c>,
///   <c>GetClipboardData</c>, <c>GlobalAlloc</c>) restricted to <c>CF_UNICODETEXT</c>.
///   Once <c>SetClipboardData</c> succeeds the OS owns the <c>HGLOBAL</c> and the data
///   persists after the calling process exits, until another application overwrites it.
///  </para>
///  <para>
///   P/Invoke surface is provided by CsWin32 (<see cref="PInvoke"/>) and is identical on
///   .NET and .NET Framework.
///  </para>
/// </remarks>
[SupportedOSPlatform("windows5.1.2600")]
internal sealed unsafe class WindowsClipboardProvider : IClipboardProvider
{
    /// <summary>
    ///  Shared instance.
    /// </summary>
    public static WindowsClipboardProvider Instance { get; } = new();

    private WindowsClipboardProvider()
    {
    }

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    public bool HasText => PInvoke.IsClipboardFormatAvailable((uint)CLIPBOARD_FORMAT.CF_UNICODETEXT);

    /// <inheritdoc/>
    public bool TryGetText([NotNullWhen(true)] out string? text)
    {
        text = null;

        if (!PInvoke.IsClipboardFormatAvailable((uint)CLIPBOARD_FORMAT.CF_UNICODETEXT))
        {
            return false;
        }

        if (!TryOpenClipboard())
        {
            return false;
        }

        try
        {
            HANDLE handle = PInvoke.GetClipboardData((uint)CLIPBOARD_FORMAT.CF_UNICODETEXT);
            if (handle.IsNull)
            {
                return false;
            }

            HGLOBAL hGlobal = (HGLOBAL)(void*)handle;
            void* pointer = PInvoke.GlobalLock(hGlobal);
            if (pointer is null)
            {
                return false;
            }

            try
            {
                nuint byteCount = PInvoke.GlobalSize(hGlobal);
                if (byteCount == 0)
                {
                    text = string.Empty;
                    return true;
                }

                // Clamp to int range. CF_UNICODETEXT is allocated by callers so this is bounded in practice.
                nuint charCount = byteCount / sizeof(char);
                int length = charCount > int.MaxValue ? int.MaxValue : (int)charCount;

                ReadOnlySpan<char> chars = new(pointer, length);
                text = chars.SliceAtNull().ToString();
                return true;
            }
            finally
            {
                PInvoke.GlobalUnlock(hGlobal);
            }
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }

    /// <inheritdoc/>
    public bool TrySetText(ReadOnlySpan<char> text)
    {
        // Compute the allocation size up front. Widen to nuint before the multiply so a
        // pathological span of int.MaxValue chars cannot overflow the size operand. The
        // checked() guards 32-bit nuint, where (int.MaxValue + 1) * 2 still overflows;
        // GlobalAlloc would have failed anyway, but we'd rather fail loudly than silently.
        // Compute this before opening the clipboard so a throw cannot leak the task lock.
        nuint bytes = checked(((nuint)text.Length + 1) * sizeof(char));

        // OpenClipboard just locks the clipboard for the current task; it is not
        // destructive, so we acquire the lock first to bail out cheaply when the
        // clipboard is held by another process. Only the EmptyClipboard call
        // below actually replaces the user's contents, and by that point the
        // HGLOBAL is fully populated and ready to hand to the OS.
        if (!TryOpenClipboard())
        {
            return false;
        }

        HGLOBAL hGlobal = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, bytes);
        if (hGlobal.IsNull)
        {
            PInvoke.CloseClipboard();
            return false;
        }

        void* pointer = PInvoke.GlobalLock(hGlobal);
        if (pointer is null)
        {
            PInvoke.GlobalFree(hGlobal);
            PInvoke.CloseClipboard();
            return false;
        }

        Span<char> destination = new(pointer, text.Length + 1);
        text.CopyTo(destination);
        destination[text.Length] = '\0';

        PInvoke.GlobalUnlock(hGlobal);

        // From here on we are committed: EmptyClipboard replaces the user's
        // contents, and any subsequent failure leaves the clipboard empty
        // rather than restored. The earlier alloc-failure paths above are the
        // ones that previously destroyed user data.
        try
        {
            if (!PInvoke.EmptyClipboard())
            {
                PInvoke.GlobalFree(hGlobal);
                return false;
            }

            HANDLE result = PInvoke.SetClipboardData((uint)CLIPBOARD_FORMAT.CF_UNICODETEXT, (HANDLE)(void*)hGlobal);
            if (result.IsNull)
            {
                // System did not take ownership; we still own the HGLOBAL.
                PInvoke.GlobalFree(hGlobal);
                return false;
            }

            return true;
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }

    /// <inheritdoc/>
    public bool TryClear()
    {
        if (!TryOpenClipboard())
        {
            return false;
        }

        try
        {
            return PInvoke.EmptyClipboard();
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }

    // OpenClipboard(NULL) is intentional. The Win32 reference pages for
    // OpenClipboard / EmptyClipboard / SetClipboardData all carry the same
    // unqualified sentence: "If an application calls OpenClipboard with hwnd
    // set to NULL, EmptyClipboard sets the clipboard owner to NULL; this
    // causes SetClipboardData to fail." Read in isolation that sentence is
    // alarming, but it only applies to delayed rendering, where
    // SetClipboardData is called with a NULL data handle and the system later
    // sends WM_RENDERFORMAT to the owner HWND to produce the bytes. No owner
    // means no recipient for that message, hence the failure. The "Using the
    // Clipboard" tutorial spells the rule out: "If a window passes a NULL
    // handle to the SetClipboardData function, it must process the
    // WM_RENDERFORMAT and WM_RENDERALLFORMATS messages to render data upon
    // request." (https://learn.microsoft.com/windows/win32/dataxchg/using-the-clipboard)
    //
    // We always pass a fully-populated HGLOBAL to SetClipboardData (immediate
    // rendering), so no callback is ever issued and the owner field is never
    // consulted for the write. The 1995-era Microsoft KB Q92530 confirms
    // this: it lists GetClipboardOwner / GetOpenClipboardWindow /
    // GetClipboardViewer / ChangeClipboardChain as the functions affected by
    // OpenClipboard(NULL), and explicitly excludes SetClipboardData.
    // Empirically the round-trip TrySetText/TryGetText test passes on every
    // supported Windows host (Windows Server 2025 CI, Windows 11 desktop),
    // matching what System.Windows.Forms.Clipboard and System.Windows.Clipboard
    // do internally for headless / non-OLE write paths.
    //
    // The belt-and-suspenders alternative is to create an HWND_MESSAGE owner
    // window for the provider to pass here. That introduces a window class
    // registration and message-pump considerations; tracking as a follow-up
    // rather than blocking this PR.
    private static bool TryOpenClipboard() => PInvoke.OpenClipboard(HWND.Null);
}
