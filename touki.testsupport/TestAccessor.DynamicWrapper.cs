// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Originally from WinForms
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Dynamic;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Touki.TestSupport;

public partial class TestAccessor<T>
{
    private sealed class DynamicWrapper : DynamicObject
    {
        private readonly object? _instance;

        public DynamicWrapper(object? instance) => _instance = instance;

        public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
        {
            ArgumentNullException.ThrowIfNull(args);
            ArgumentNullException.ThrowIfNull(binder);

            result = null;

            MethodInfo? methodInfo = null;
            Type? type = s_type;

            do
            {
                try
                {
                    methodInfo = type?.GetMethod(
                        binder.Name,
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                }
                catch (AmbiguousMatchException)
                {
                    // More than one match for the name, specify the arguments.
                    // Reflection overload resolution requires a runtime type for every argument.
                    methodInfo = type?.GetMethod(
                        binder.Name,
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                        binder: null,
                        [.. args.Select(a => a is not null
                            ? a.GetType()
                            : throw new ArgumentException(
                                "Null arguments are not supported when resolving overloaded methods.",
                                nameof(args)))],
                        modifiers: null);
                }

                if (methodInfo is not null || type == typeof(object))
                {
                    // Found something, or already at the top of the type hierarchy
                    break;
                }

                // Walk up the hierarchy
                type = type?.BaseType;
            }
            while (true);

            if (methodInfo is null)
            {
                return false;
            }

            try
            {
                result = methodInfo.Invoke(_instance, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                // Unwrap the inner exception to make it easier for callers to handle.
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }

            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object? value)
        {
            if (GetFieldOrPropertyInfo(binder.Name) is not { } memberInfo)
            {
                return false;
            }

            try
            {
                switch (memberInfo)
                {
                    case FieldInfo fieldInfo:
                        fieldInfo.SetValue(_instance, value);
                        break;
                    case PropertyInfo propertyInfo:
                        propertyInfo.SetValue(_instance, value);
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                // Unwrap the inner exception to make it easier for callers to handle.
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }

            return true;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            result = null;

            if (GetFieldOrPropertyInfo(binder.Name) is not { } memberInfo)
            {
                return false;
            }

            try
            {
                result = memberInfo switch
                {
                    FieldInfo fieldInfo => fieldInfo.GetValue(_instance),
                    PropertyInfo propertyInfo => propertyInfo.GetValue(_instance),
                    _ => throw new InvalidOperationException()
                };
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                // Unwrap the inner exception to make it easier for callers to handle.
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }

            return true;
        }

        private static MemberInfo? GetFieldOrPropertyInfo(string memberName)
        {
            Type? type = s_type;
            MemberInfo? info;

            do
            {
                info = (MemberInfo?)type?.GetField(
                    memberName,
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? type?.GetProperty(
                        memberName,
                        BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);

                if (info is not null || type == typeof(object))
                {
                    // Found something, or already at the top of the type hierarchy
                    break;
                }

                // Walk up the type hierarchy
                type = type?.BaseType;
            }
            while (true);

            return info;
        }
    }
}
