// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// IDE0380 correctly reports most of these unsafe contexts as unnecessary in modern .NET. The same
// source must retain them for its .NET Framework build, which per-target analysis cannot account for.
// The pinned SDK also misreports the assembly reader's required pointer context on modern .NET.
// Suppress each modern report individually rather than weakening the rule for all modern .NET code.

#if NET

[assembly: SuppressMessage(
    "Style",
    "IDE0380:Remove unnecessary 'unsafe' modifier",
    Justification = "Required by the .NET Framework target of this multi-targeted build.",
    Scope = "type",
    Target = "~T:Touki.Buffers.BorrowedMemoryManager")]

[assembly: SuppressMessage(
    "Style",
    "IDE0380:Remove unnecessary 'unsafe' modifier",
    Justification = "Required by the .NET Framework target of this multi-targeted build.",
    Scope = "member",
    Target = "~M:Touki.SpanExtensions.CompareOrdinalIgnoreCaseAsciiFold(System.ReadOnlySpan{System.Char},"
        + "System.ReadOnlySpan{System.Char})~System.Int32")]

[assembly: SuppressMessage(
    "Style",
    "IDE0380:Remove unnecessary 'unsafe' modifier",
    Justification = "Required by the .NET Framework target of this multi-targeted build.",
    Scope = "type",
    Target = "~T:Touki.Io.MappedMemoryManager")]

[assembly: SuppressMessage(
    "Style",
    "IDE0380:Remove unnecessary 'unsafe' modifier",
    Justification = "Required by the .NET Framework target of this multi-targeted build.",
    Scope = "type",
    Target = "~T:Touki.Io.MappedViewLease")]

[assembly: SuppressMessage(
    "Style",
    "IDE0380:Remove unnecessary 'unsafe' modifier",
    Justification = "The assembly reader's pointer cast requires an unsafe context on all targets.",
    Scope = "member",
    Target = "~M:Touki.Resources.StringResourceTableLoader.LoadIndexedTableFromAssemblyCore(System."
        + "ReadOnlyMemory{System.Byte},System.String,Touki.Resources.StringResourceManagerOptions,"
        + "System.IDisposable,System.Boolean,Touki.Resources.ResourceAssemblyMetadata,System.String,"
        + "Touki.Resources.ManagedAssemblyIdentity)")]

[assembly: SuppressMessage(
    "Style",
    "IDE0380:Remove unnecessary 'unsafe' modifier",
    Justification = "Required by the .NET Framework target of this multi-targeted build.",
    Scope = "member",
    Target = "~M:Touki.Resources.StringResourceTableLoader.LoadTableFromAssembly(System.ReadOnlyMemory{System."
        + "Byte},System.String,Touki.Resources.StringResourceManagerOptions)~Touki.Resources."
        + "StringResourceTable")]

[assembly: SuppressMessage(
    "Style",
    "IDE0380:Remove unnecessary 'unsafe' modifier",
    Justification = "Required by the .NET Framework target of this multi-targeted build.",
    Scope = "member",
    Target = "~M:Touki.Resources.StringResourceTableLoader.CreateStringResourceReader(System.IO.Stream)~Touki.Resources.IStringResourceReader")]

[assembly: SuppressMessage(
    "Style",
    "IDE0380:Remove unnecessary 'unsafe' modifier",
    Justification = "Required by the .NET Framework target of this multi-targeted build.",
    Scope = "type",
    Target = "~T:Touki.Text.StringBuilderExtensions")]

[assembly: SuppressMessage(
    "Style",
    "IDE0380:Remove unnecessary 'unsafe' modifier",
    Justification = "Required by the .NET Framework target of this multi-targeted build.",
    Scope = "member",
    Target = "~M:Touki.Text.StringSegment.Replace(System.Char,System.Char)~Touki.Text.StringSegment")]

[assembly: SuppressMessage(
    "Style",
    "IDE0380:Remove unnecessary 'unsafe' modifier",
    Justification = "Required by the .NET Framework target of this multi-targeted build.",
    Scope = "member",
    Target = "~M:Touki.Text.StringSegment.GetPinnableReference~System.Char")]

[assembly: SuppressMessage(
    "Style",
    "IDE0380:Remove unnecessary 'unsafe' modifier",
    Justification = "Required by the .NET Framework target of this multi-targeted build.",
    Scope = "member",
    Target = "~M:Touki.Text.StringSegment.CompareToOrdinalIgnoreCase(System.String,System.Int32,System.Int32)~System.Int32")]

[assembly: SuppressMessage(
    "Style",
    "IDE0380:Remove unnecessary 'unsafe' modifier",
    Justification = "Required by the .NET Framework target of this multi-targeted build.",
    Scope = "member",
    Target = "~M:Touki.Text.ValueStringBuilder.AppendFormat``1(System.ReadOnlySpan{System.Char},``0)")]

[assembly: SuppressMessage(
    "Style",
    "IDE0380:Remove unnecessary 'unsafe' modifier",
    Justification = "Required by the .NET Framework target of this multi-targeted build.",
    Scope = "type",
    Target = "~T:Touki.Io.Providers.WindowsClipboardProvider")]

#endif

// Constants that mirror a native header keep the header's spelling so they stay greppable against the
// platform documentation and the C declaration they were transcribed from. That spelling is not
// PascalCase, so the naming rule has to be waived one constant at a time. This is deliberately a
// per-symbol suppression rather than an .editorconfig rule: there is nothing on the symbol to match on,
// so a rule would have to exempt a whole file or every constant, and either would hide real violations.
//
// The P/Invoke declarations these constants are passed to are exempt through a real naming rule instead,
// because [LibraryImport] and [DllImport] give it something to match on. See .editorconfig.
//
// Guarded because both providers are '#if NET'. On the framework target the targets below do not resolve
// and IDE0076 reports each one as an invalid target.

#if NET

[assembly: SuppressMessage(
    "Naming",
    "TOUKI0041:Naming rule violation",
    Justification = "Mirrors RTLD_LAZY from <dlfcn.h>.",
    Scope = "member",
    Target = "~F:Touki.Io.Providers.MacClipboardProvider.RTLD_LAZY")]

[assembly: SuppressMessage(
    "Naming",
    "TOUKI0041:Naming rule violation",
    Justification = "Mirrors RTLD_GLOBAL from <dlfcn.h>.",
    Scope = "member",
    Target = "~F:Touki.Io.Providers.MacClipboardProvider.RTLD_GLOBAL")]

[assembly: SuppressMessage(
    "Naming",
    "TOUKI0041:Naming rule violation",
    Justification = "Mirrors X_OK from <unistd.h>.",
    Scope = "member",
    Target = "~F:Touki.Io.Providers.LinuxClipboardProvider.X_OK")]

#endif
