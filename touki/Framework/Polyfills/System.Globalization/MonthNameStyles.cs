// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Globalization;

/// <summary>
///  Flags used to indicate different styles of month names.
/// </summary>
internal enum MonthNameStyles
{
    Regular = 0x00000000,
    Genitive = 0x00000001,
    LeapYear = 0x00000002,
}
