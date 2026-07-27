// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

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
