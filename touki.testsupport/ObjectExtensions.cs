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
///  Object extension methods used for testing.
/// </summary>
/// <remarks>
///  <para>In the System namespace for implicit discovery.</para>
/// </remarks>
public static partial class ObjectExtensions
{
    // Need to pass a null parameter when constructing a static instance
    // of TestAccessor. As this is pretty common and never changes, caching
    // the array here.
    private static readonly object?[] s_nullObjectParam = [null];

    /// <param name="instanceOrType">
    ///  Instance or Type class (if only accessing statics).
    /// </param>
    extension(object instanceOrType)
    {
        /// <summary>
        ///  Extension that creates a generic internals test accessor for a
        ///  given instance or <see cref="Type"/> class (if only accessing statics).
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   Use <see cref="ITestAccessor.CreateDelegate">CreateDelegate</see> to deal with methods that take spans or
        ///   other ref structs. For other members, use the dynamic accessor:
        ///  </para>
        ///  <code>
        ///   <![CDATA[
        ///   Version version = new Version(4, 1);
        ///    Assert.Equal(4, version.TestAccessor.Dynamic._Major));
        ///
        ///    // Or
        ///
        ///    dynamic accessor = version.TestAccessor.Dynamic;
        ///    Assert.Equal(4, accessor._Major));
        ///
        ///    // Or
        ///
        ///    Version version2 = new Version("4.1");
        ///    dynamic accessor = typeof(Version).TestAccessor.Dynamic;
        ///    Assert.Equal(version2, accessor.Parse("4.1")));
        ///   ]]>
        ///  </code>
        /// </remarks>
        public ITestAccessor TestAccessor
        {
            get
            {
                ITestAccessor? testAccessor = instanceOrType is Type type
                    ? (ITestAccessor?)Activator.CreateInstance(
                        typeof(TestAccessor<>).MakeGenericType(type),
                        s_nullObjectParam)
                    : (ITestAccessor?)Activator.CreateInstance(
                        typeof(TestAccessor<>).MakeGenericType(instanceOrType.GetType()),
                        instanceOrType);

                return testAccessor
                    ?? throw new ArgumentException("Cannot create TestAccessor for Nullable<T> instances with no value.");
            }
        }
    }

    extension(object @object)
    {
        /// <summary>
        ///  Invokes the finalizer of an object directly, bypassing the normal garbage collection process.
        /// </summary>
        public void InvokeFinalizer()
        {
            // Find the special finalizer method and invoke it directly
            MethodInfo? finalizerMethod = @object.GetType().GetMethod(
                "Finalize",
                BindingFlags.NonPublic | BindingFlags.Instance);

            finalizerMethod?.Invoke(@object, null);
        }

    }
}
