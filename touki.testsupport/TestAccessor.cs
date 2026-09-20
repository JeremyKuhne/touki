// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Originally from WinForms
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace Touki.TestSupport;

/// <summary>
///  Internals (including privates) access wrapper for tests.
/// </summary>
/// <typeparam name="T">The type of the class being accessed.</typeparam>
/// <remarks>
///  <para>
///   Does not allow access to public members- use the object directly.
///  </para>
///  <para>
///   One should strive to *not* access internal state where otherwise avoidable.
///   Ask yourself if you can test the contract of the object in question
///   *without* manipulating internals directly. Often you can.
///  </para>
///  <para>
///   Where internals access is more useful are testing building blocks of more
///   complicated objects, such as internal helper methods or classes.
///  </para>
/// </remarks>
/// <example>
///  This class can also be derived from to create a strongly typed wrapper
///  that can then be associated via an extension method for the given type
///  to provide consistent discovery and access.
///
///  <![CDATA[
///   public class GuidTestAccessor : TestAccessor<Guid>
///   {
///     public TestAccessor(Guid instance) : base(instance) {}
///
///     public int A => Dynamic._a;
///   }
///
///   public static partial class TestAccessors
///   {
///       public static GuidTestAccessor TestAccessor(this Guid guid)
///           => new GuidTestAccessor(guid);
///   }
///  ]]>
/// </example>
public partial class TestAccessor<T> : ITestAccessor
{
    private static readonly Type s_type = typeof(T);
    private readonly T? _instance;
    private readonly DynamicWrapper _dynamicWrapper;

    /// <param name="instance">The type instance, can be null for statics.</param>
    public TestAccessor(T? instance)
    {
        _instance = instance;
        _dynamicWrapper = new DynamicWrapper(_instance);
    }

    /// <inheritdoc/>
    public TDelegate CreateDelegate<TDelegate>(string? methodName = null)
        where TDelegate : Delegate
    {
        Type type = typeof(TDelegate);
        MethodInfo? invokeMethodInfo = type.GetMethod("Invoke");
        Type[] types = invokeMethodInfo is null ? [] : [.. invokeMethodInfo.GetParameters().Select(pi => pi.ParameterType)];

        // To make it easier to write a class wrapper with a number of delegates,
        // we'll take the name from the delegate itself when unspecified.
        methodName ??= type.Name;

        MethodInfo? methodInfo = s_type.GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
            binder: null,
            types,
            modifiers: null) ?? throw new ArgumentException($"Could not find non public method {methodName}.");

        return (TDelegate)methodInfo.CreateDelegate(type, methodInfo.IsStatic ? null : _instance);
    }

    /// <inheritdoc/>
    public dynamic Dynamic => _dynamicWrapper;
}
