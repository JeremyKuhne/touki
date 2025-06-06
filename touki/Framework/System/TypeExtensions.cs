// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System;

internal static class TypeExtensions
{
    public static bool IsAssignableTo(this Type? type, Type? targetType) =>
       targetType?.IsAssignableFrom(type) ?? false;
}
