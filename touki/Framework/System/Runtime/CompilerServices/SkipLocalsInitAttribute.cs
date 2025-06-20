﻿// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Runtime.CompilerServices;

/// <summary>
/// Used to indicate to the compiler that the <c>.locals init</c>
/// flag should not be set in method headers.
/// </summary>
/// <remarks>
/// This attribute is unsafe because it may reveal uninitialized memory to
/// the application in certain instances (e.g., reading from uninitialized
/// stackalloc'd memory). If applied to a method directly, the attribute
/// applies to that method and all nested functions (lambdas, local
/// functions) below it. If applied to a type or module, it applies to all
/// methods nested inside. This attribute is intentionally not permitted on
/// assemblies. Use at the module level instead to apply to multiple type
/// declarations.
/// </remarks>
[AttributeUsage(AttributeTargets.Module
    | AttributeTargets.Class
    | AttributeTargets.Struct
    | AttributeTargets.Interface
    | AttributeTargets.Constructor
    | AttributeTargets.Method
    | AttributeTargets.Property
    | AttributeTargets.Event, Inherited = false)]
public sealed class SkipLocalsInitAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SkipLocalsInitAttribute"/> class.
    /// </summary>
    public SkipLocalsInitAttribute()
    {
    }
}
