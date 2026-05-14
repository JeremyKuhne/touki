// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET

using System.Runtime.Versioning;
using System.Text;

namespace Touki.Io.Providers;

/// <summary>
///  macOS clipboard transport for <see cref="Clipboard"/>.
/// </summary>
/// <remarks>
///  <para>
///   Calls the system <c>NSPasteboard</c> via the Objective-C runtime
///   (<c>libobjc.dylib</c>). The pasteboard is hosted by the per-user
///   <c>pasteboardd</c> daemon, so text written here persists after the
///   calling process exits, until another application overwrites it
///   or the system is restarted.
///  </para>
///  <para>
///   The system framework <c>NSPasteboardTypeString</c> is defined as
///   the UTI <c>public.utf8-plain-text</c>. The provider hardcodes that
///   UTI rather than resolving the extern symbol from AppKit.
///  </para>
/// </remarks>
[SupportedOSPlatform("macos")]
internal sealed unsafe partial class MacClipboardProvider : IClipboardProvider
{
    /// <summary>
    ///  Shared instance.
    /// </summary>
    public static MacClipboardProvider Instance { get; } = new();

    private const string ObjC = "/usr/lib/libobjc.dylib";

    // NSPasteboardTypeString = @"public.utf8-plain-text" since OS X 10.6.
    private const string PasteboardTypeString = "public.utf8-plain-text";

    // Lazily-resolved class and selector handles. Resolution is idempotent — the
    // Objective-C runtime returns the same pointer for repeated lookups.
    private static readonly nint s_nsPasteboardClass = objc_getClass("NSPasteboard"u8);
    private static readonly nint s_nsStringClass = objc_getClass("NSString"u8);
    private static readonly nint s_selGeneralPasteboard = sel_registerName("generalPasteboard"u8);
    private static readonly nint s_selClearContents = sel_registerName("clearContents"u8);
    private static readonly nint s_selSetStringForType = sel_registerName("setString:forType:"u8);
    private static readonly nint s_selStringForType = sel_registerName("stringForType:"u8);
    private static readonly nint s_selAvailableTypeFromArray = sel_registerName("availableTypeFromArray:"u8);
    private static readonly nint s_selStringWithUTF8String = sel_registerName("stringWithUTF8String:"u8);
    private static readonly nint s_selUTF8String = sel_registerName("UTF8String"u8);
    private static readonly nint s_selArrayWithObject = sel_registerName("arrayWithObject:"u8);
    private static readonly nint s_nsArrayClass = objc_getClass("NSArray"u8);

    private MacClipboardProvider()
    {
    }

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    public bool HasText
    {
        get
        {
            nint pool = objc_autoreleasePoolPush();
            try
            {
                nint pasteboard = GeneralPasteboard();
                if (pasteboard == 0)
                {
                    return false;
                }

                nint typeString = MakeNSString(PasteboardTypeString);
                if (typeString == 0)
                {
                    return false;
                }

                // [NSArray arrayWithObject:typeString]
                nint typeArray = objc_msgSend_id_id(s_nsArrayClass, s_selArrayWithObject, typeString);
                if (typeArray == 0)
                {
                    return false;
                }

                // [pasteboard availableTypeFromArray:typeArray]
                nint match = objc_msgSend_id_id(pasteboard, s_selAvailableTypeFromArray, typeArray);
                return match != 0;
            }
            finally
            {
                objc_autoreleasePoolPop(pool);
            }
        }
    }

    /// <inheritdoc/>
    public bool TryGetText([NotNullWhen(true)] out string? text)
    {
        text = null;

        nint pool = objc_autoreleasePoolPush();
        try
        {
            nint pasteboard = GeneralPasteboard();
            if (pasteboard == 0)
            {
                return false;
            }

            nint typeString = MakeNSString(PasteboardTypeString);
            if (typeString == 0)
            {
                return false;
            }

            // [pasteboard stringForType:typeString]
            nint result = objc_msgSend_id_id(pasteboard, s_selStringForType, typeString);
            if (result == 0)
            {
                return false;
            }

            // const char* utf8 = [result UTF8String];
            nint utf8 = objc_msgSend_id(result, s_selUTF8String);
            if (utf8 == 0)
            {
                text = string.Empty;
                return true;
            }

            text = Marshal.PtrToStringUTF8(utf8) ?? string.Empty;
            return true;
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    /// <inheritdoc/>
    public bool TrySetText(ReadOnlySpan<char> text)
    {
        nint pool = objc_autoreleasePoolPush();
        try
        {
            nint pasteboard = GeneralPasteboard();
            if (pasteboard == 0)
            {
                return false;
            }

            // [pasteboard clearContents] — ignore the returned NSInteger changeCount.
            objc_msgSend_void(pasteboard, s_selClearContents);

            // NSString *value = [NSString stringWithUTF8String:utf8];
            nint nsValue = MakeNSString(text);
            if (nsValue == 0)
            {
                return false;
            }

            nint nsType = MakeNSString(PasteboardTypeString);
            if (nsType == 0)
            {
                return false;
            }

            // BOOL ok = [pasteboard setString:value forType:type]
            return objc_msgSend_bool_id_id(pasteboard, s_selSetStringForType, nsValue, nsType);
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    /// <inheritdoc/>
    public bool TryClear()
    {
        nint pool = objc_autoreleasePoolPush();
        try
        {
            nint pasteboard = GeneralPasteboard();
            if (pasteboard == 0)
            {
                return false;
            }

            objc_msgSend_void(pasteboard, s_selClearContents);
            return true;
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    private static nint GeneralPasteboard()
        => objc_msgSend_id(s_nsPasteboardClass, s_selGeneralPasteboard);

    /// <summary>
    ///  Encodes <paramref name="value"/> as a NUL-terminated UTF-8 byte sequence on the
    ///  stack (with a pool fallback for very long payloads) and returns the result of
    ///  <c>[NSString stringWithUTF8String:utf8]</c>. The returned NSString is autoreleased
    ///  - callers must hold an autorelease pool while it is in use.
    /// </summary>
    private static nint MakeNSString(ReadOnlySpan<char> value)
    {
        int max = Encoding.UTF8.GetMaxByteCount(value.Length) + 1;

        // 256 bytes covers every common pasteboard-type UTI (the constant case) and any
        // short user text up to ~85 ASCII characters or ~64 non-ASCII characters. Larger
        // text falls through to ArrayPool inside BufferScope.
        using BufferScope<byte> bytes = new(stackalloc byte[256], max);
        int written = Encoding.UTF8.GetBytes(value, bytes);
        bytes[written] = 0;

        fixed (byte* ptr = bytes)
        {
            return objc_msgSend_id_ptr(s_nsStringClass, s_selStringWithUTF8String, (nint)ptr);
        }
    }

    // P/Invoke surface. UTF-8 string literals are stable null-terminated byte sequences
    // suitable for the C strings these runtime calls expect.

    [LibraryImport(ObjC, EntryPoint = "objc_getClass")]
    private static partial nint objc_getClass(ReadOnlySpan<byte> name);

    [LibraryImport(ObjC, EntryPoint = "sel_registerName")]
    private static partial nint sel_registerName(ReadOnlySpan<byte> name);

    [LibraryImport(ObjC, EntryPoint = "objc_autoreleasePoolPush")]
    private static partial nint objc_autoreleasePoolPush();

    [LibraryImport(ObjC, EntryPoint = "objc_autoreleasePoolPop")]
    private static partial void objc_autoreleasePoolPop(nint context);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial nint objc_msgSend_id(nint receiver, nint selector);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial void objc_msgSend_void(nint receiver, nint selector);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial nint objc_msgSend_id_id(nint receiver, nint selector, nint arg1);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial nint objc_msgSend_id_ptr(nint receiver, nint selector, nint arg1);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool objc_msgSend_bool_id_id(nint receiver, nint selector, nint arg1, nint arg2);
}

#endif
