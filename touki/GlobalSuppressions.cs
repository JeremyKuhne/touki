// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Thread-static fields used to need an IDE1006 suppression each: the built-in naming rules match on
// modifiers, so a [ThreadStatic] field matches the ordinary static rule and is reported for missing the
// 's_' prefix (dotnet/roslyn#32955). Touki.Analyzers.ThreadStaticNamingSuppressor now suppresses IDE1006
// for those fields and TOUKI0040 enforces the 't_' prefix, so no entries are needed here.
