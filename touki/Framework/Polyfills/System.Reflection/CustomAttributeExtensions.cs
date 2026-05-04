// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Reflection;

/// <summary>
///  Contains extension methods for retrieving custom attributes.
/// </summary>
public static class CustomAttributeExtensions
{
    #region APIs that return a single attribute
    /// <summary>
    ///  Retrieves a custom attribute of a specified type that is applied to a specified assembly.
    /// </summary>
    /// <param name="element">The assembly to inspect.</param>
    /// <param name="attributeType">The type of attribute to search for.</param>
    /// <returns>A custom attribute that matches <paramref name="attributeType"/>, or <see langword="null"/> if no such attribute is found.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="element"/> or <paramref name="attributeType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="attributeType"/> is not derived from <see cref="Attribute"/>.</exception>
    /// <exception cref="AmbiguousMatchException">More than one of the requested attributes was found.</exception>
    public static Attribute? GetCustomAttribute(this Assembly element, Type attributeType)
    {
        return Attribute.GetCustomAttribute(element, attributeType);
    }

    /// <inheritdoc cref="GetCustomAttribute(Assembly, Type)"/>
    /// <summary>
    ///  Retrieves a custom attribute of a specified type that is applied to a specified module.
    /// </summary>
    /// <param name="element">The module to inspect.</param>
    public static Attribute? GetCustomAttribute(this Module element, Type attributeType)
    {
        return Attribute.GetCustomAttribute(element, attributeType);
    }

    /// <inheritdoc cref="GetCustomAttribute(ParameterInfo, Type)"/>
    /// <summary>
    ///  Retrieves a custom attribute of a specified type that is applied to a specified member.
    /// </summary>
    /// <param name="element">The member to inspect.</param>
    /// <returns>A custom attribute that matches <paramref name="attributeType"/>, or <see langword="null"/> if no such attribute is found.</returns>
    /// <exception cref="NotSupportedException"><paramref name="element"/> is not a constructor, method, property, event, type, or field.</exception>
    public static Attribute? GetCustomAttribute(this MemberInfo element, Type attributeType)
    {
        return Attribute.GetCustomAttribute(element, attributeType);
    }

    /// <inheritdoc cref="GetCustomAttribute(Assembly, Type)"/>
    /// <summary>
    ///  Retrieves a custom attribute of a specified type that is applied to a specified parameter.
    /// </summary>
    /// <param name="element">The parameter to inspect.</param>
    /// <param name="attributeType">The type of attribute to search for.</param>
    /// <returns>A custom attribute that matches <paramref name="attributeType"/>, or <see langword="null"/> if no such attribute is found.</returns>
    /// <exception cref="TypeLoadException">A custom attribute type cannot be loaded.</exception>
    public static Attribute? GetCustomAttribute(this ParameterInfo element, Type attributeType)
    {
        return Attribute.GetCustomAttribute(element, attributeType);
    }

    /// <inheritdoc cref="GetCustomAttribute(Assembly, Type)"/>
    /// <typeparam name="T">The type of attribute to search for.</typeparam>
    public static T? GetCustomAttribute<T>(this Assembly element) where T : Attribute
    {
        return (T?)GetCustomAttribute(element, typeof(T));
    }

    /// <inheritdoc cref="GetCustomAttribute(Module, Type)"/>
    /// <typeparam name="T">The type of attribute to search for.</typeparam>
    public static T? GetCustomAttribute<T>(this Module element) where T : Attribute
    {
        return (T?)GetCustomAttribute(element, typeof(T));
    }

    /// <inheritdoc cref="GetCustomAttribute(MemberInfo, Type)"/>
    /// <typeparam name="T">The type of attribute to search for.</typeparam>
    public static T? GetCustomAttribute<T>(this MemberInfo element) where T : Attribute
    {
        return (T?)GetCustomAttribute(element, typeof(T));
    }

    /// <inheritdoc cref="GetCustomAttribute(ParameterInfo, Type)"/>
    /// <typeparam name="T">The type of attribute to search for.</typeparam>
    public static T? GetCustomAttribute<T>(this ParameterInfo element) where T : Attribute
    {
        return (T?)GetCustomAttribute(element, typeof(T));
    }

    /// <inheritdoc cref="GetCustomAttribute(MemberInfo, Type)"/>
    /// <param name="inherit"><see langword="true"/> to inspect the ancestors of <paramref name="element"/>; otherwise, <see langword="false"/>.</param>
    public static Attribute? GetCustomAttribute(this MemberInfo element, Type attributeType, bool inherit)
    {
        return Attribute.GetCustomAttribute(element, attributeType, inherit);
    }

    /// <inheritdoc cref="GetCustomAttribute(ParameterInfo, Type)"/>
    /// <param name="inherit"><see langword="true"/> to inspect the ancestors of <paramref name="element"/>; otherwise, <see langword="false"/>.</param>
    public static Attribute? GetCustomAttribute(this ParameterInfo element, Type attributeType, bool inherit)
    {
        return Attribute.GetCustomAttribute(element, attributeType, inherit);
    }

    /// <inheritdoc cref="GetCustomAttribute(MemberInfo, Type)"/>
    /// <typeparam name="T">The type of attribute to search for.</typeparam>
    /// <param name="inherit"><see langword="true"/> to inspect the ancestors of <paramref name="element"/>; otherwise, <see langword="false"/>.</param>
    public static T? GetCustomAttribute<T>(this MemberInfo element, bool inherit) where T : Attribute
    {
        return (T?)GetCustomAttribute(element, typeof(T), inherit);
    }

    /// <inheritdoc cref="GetCustomAttribute(ParameterInfo, Type)"/>
    /// <typeparam name="T">The type of attribute to search for.</typeparam>
    /// <param name="inherit"><see langword="true"/> to inspect the ancestors of <paramref name="element"/>; otherwise, <see langword="false"/>.</param>
    public static T? GetCustomAttribute<T>(this ParameterInfo element, bool inherit) where T : Attribute
    {
        return (T?)GetCustomAttribute(element, typeof(T), inherit);
    }
    #endregion

    #region APIs that return all attributes
    /// <summary>
    ///  Retrieves a collection of custom attributes that are applied to a specified assembly.
    /// </summary>
    /// <param name="element">The assembly to inspect.</param>
    /// <returns>A collection of the custom attributes that are applied to <paramref name="element"/>, or an empty collection if no such attributes exist.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="element"/> is <see langword="null"/>.</exception>
    public static IEnumerable<Attribute> GetCustomAttributes(this Assembly element)
    {
        return Attribute.GetCustomAttributes(element);
    }

    /// <summary>
    ///  Retrieves a collection of custom attributes that are applied to a specified module.
    /// </summary>
    /// <param name="element">The module to inspect.</param>
    /// <returns>A collection of the custom attributes that are applied to <paramref name="element"/>, or an empty collection if no such attributes exist.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="element"/> is <see langword="null"/>.</exception>
    public static IEnumerable<Attribute> GetCustomAttributes(this Module element)
    {
        return Attribute.GetCustomAttributes(element);
    }

    /// <summary>
    ///  Retrieves a collection of custom attributes that are applied to a specified member.
    /// </summary>
    /// <param name="element">The member to inspect.</param>
    /// <returns>A collection of the custom attributes that are applied to <paramref name="element"/>, or an empty collection if no such attributes exist.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="element"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException"><paramref name="element"/> is not a constructor, method, property, event, type, or field.</exception>
    /// <exception cref="TypeLoadException">A custom attribute type cannot be loaded.</exception>
    public static IEnumerable<Attribute> GetCustomAttributes(this MemberInfo element)
    {
        return Attribute.GetCustomAttributes(element);
    }

    /// <summary>
    ///  Retrieves a collection of custom attributes that are applied to a specified parameter.
    /// </summary>
    /// <param name="element">The parameter to inspect.</param>
    /// <returns>A collection of the custom attributes that are applied to <paramref name="element"/>, or an empty collection if no such attributes exist.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="element"/> is <see langword="null"/>.</exception>
    /// <exception cref="TypeLoadException">A custom attribute type cannot be loaded.</exception>
    public static IEnumerable<Attribute> GetCustomAttributes(this ParameterInfo element)
    {
        return Attribute.GetCustomAttributes(element);
    }

    /// <inheritdoc cref="GetCustomAttributes(MemberInfo)"/>
    /// <param name="inherit"><see langword="true"/> to inspect the ancestors of <paramref name="element"/>; otherwise, <see langword="false"/>.</param>
    public static IEnumerable<Attribute> GetCustomAttributes(this MemberInfo element, bool inherit)
    {
        return Attribute.GetCustomAttributes(element, inherit);
    }

    /// <inheritdoc cref="GetCustomAttributes(ParameterInfo)"/>
    /// <param name="inherit"><see langword="true"/> to inspect the ancestors of <paramref name="element"/>; otherwise, <see langword="false"/>.</param>
    public static IEnumerable<Attribute> GetCustomAttributes(this ParameterInfo element, bool inherit)
    {
        return Attribute.GetCustomAttributes(element, inherit);
    }
    #endregion

    #region APIs that return all attributes of a particular type
    /// <summary>
    ///  Retrieves a collection of custom attributes of a specified type that are applied to a specified assembly.
    /// </summary>
    /// <param name="element">The assembly to inspect.</param>
    /// <param name="attributeType">The type of attribute to search for.</param>
    /// <returns>
    ///  A collection of the custom attributes that are applied to <paramref name="element"/> and that match
    ///  <paramref name="attributeType"/>, or an empty collection if no such attributes exist.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="element"/> or <paramref name="attributeType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="attributeType"/> is not derived from <see cref="Attribute"/>.</exception>
    public static IEnumerable<Attribute> GetCustomAttributes(this Assembly element, Type attributeType)
    {
        return Attribute.GetCustomAttributes(element, attributeType);
    }

    /// <inheritdoc cref="GetCustomAttributes(Assembly, Type)"/>
    /// <summary>
    ///  Retrieves a collection of custom attributes of a specified type that are applied to a specified module.
    /// </summary>
    /// <param name="element">The module to inspect.</param>
    public static IEnumerable<Attribute> GetCustomAttributes(this Module element, Type attributeType)
    {
        return Attribute.GetCustomAttributes(element, attributeType);
    }

    /// <inheritdoc cref="GetCustomAttributes(ParameterInfo, Type)"/>
    /// <summary>
    ///  Retrieves a collection of custom attributes of a specified type that are applied to a specified member.
    /// </summary>
    /// <param name="element">The member to inspect.</param>
    /// <exception cref="NotSupportedException"><paramref name="element"/> is not a constructor, method, property, event, type, or field.</exception>
    public static IEnumerable<Attribute> GetCustomAttributes(this MemberInfo element, Type attributeType)
    {
        return Attribute.GetCustomAttributes(element, attributeType);
    }

    /// <inheritdoc cref="GetCustomAttributes(Assembly, Type)"/>
    /// <summary>
    ///  Retrieves a collection of custom attributes of a specified type that are applied to a specified parameter.
    /// </summary>
    /// <param name="element">The parameter to inspect.</param>
    /// <exception cref="TypeLoadException">A custom attribute type cannot be loaded.</exception>
    public static IEnumerable<Attribute> GetCustomAttributes(this ParameterInfo element, Type attributeType)
    {
        return Attribute.GetCustomAttributes(element, attributeType);
    }

    /// <inheritdoc cref="GetCustomAttributes(Assembly, Type)"/>
    /// <typeparam name="T">The type of attribute to search for.</typeparam>
    public static IEnumerable<T> GetCustomAttributes<T>(this Assembly element) where T : Attribute
    {
        return (IEnumerable<T>)GetCustomAttributes(element, typeof(T));
    }

    /// <inheritdoc cref="GetCustomAttributes(Module, Type)"/>
    /// <typeparam name="T">The type of attribute to search for.</typeparam>
    public static IEnumerable<T> GetCustomAttributes<T>(this Module element) where T : Attribute
    {
        return (IEnumerable<T>)GetCustomAttributes(element, typeof(T));
    }

    /// <inheritdoc cref="GetCustomAttributes(MemberInfo, Type)"/>
    /// <typeparam name="T">The type of attribute to search for.</typeparam>
    public static IEnumerable<T> GetCustomAttributes<T>(this MemberInfo element) where T : Attribute
    {
        return (IEnumerable<T>)GetCustomAttributes(element, typeof(T));
    }

    /// <inheritdoc cref="GetCustomAttributes(ParameterInfo, Type)"/>
    /// <typeparam name="T">The type of attribute to search for.</typeparam>
    public static IEnumerable<T> GetCustomAttributes<T>(this ParameterInfo element) where T : Attribute
    {
        return (IEnumerable<T>)GetCustomAttributes(element, typeof(T));
    }

    /// <inheritdoc cref="GetCustomAttributes(MemberInfo, Type)"/>
    /// <param name="inherit"><see langword="true"/> to inspect the ancestors of <paramref name="element"/>; otherwise, <see langword="false"/>.</param>
    public static IEnumerable<Attribute> GetCustomAttributes(this MemberInfo element, Type attributeType, bool inherit)
    {
        return Attribute.GetCustomAttributes(element, attributeType, inherit);
    }

    /// <inheritdoc cref="GetCustomAttributes(ParameterInfo, Type)"/>
    /// <param name="inherit"><see langword="true"/> to inspect the ancestors of <paramref name="element"/>; otherwise, <see langword="false"/>.</param>
    public static IEnumerable<Attribute> GetCustomAttributes(this ParameterInfo element, Type attributeType, bool inherit)
    {
        return Attribute.GetCustomAttributes(element, attributeType, inherit);
    }

    /// <inheritdoc cref="GetCustomAttributes(MemberInfo, Type)"/>
    /// <typeparam name="T">The type of attribute to search for.</typeparam>
    /// <param name="inherit"><see langword="true"/> to inspect the ancestors of <paramref name="element"/>; otherwise, <see langword="false"/>.</param>
    public static IEnumerable<T> GetCustomAttributes<T>(this MemberInfo element, bool inherit) where T : Attribute
    {
        return (IEnumerable<T>)GetCustomAttributes(element, typeof(T), inherit);
    }

    /// <inheritdoc cref="GetCustomAttributes(ParameterInfo, Type)"/>
    /// <typeparam name="T">The type of attribute to search for.</typeparam>
    /// <param name="inherit"><see langword="true"/> to inspect the ancestors of <paramref name="element"/>; otherwise, <see langword="false"/>.</param>
    public static IEnumerable<T> GetCustomAttributes<T>(this ParameterInfo element, bool inherit) where T : Attribute
    {
        return (IEnumerable<T>)GetCustomAttributes(element, typeof(T), inherit);
    }
    #endregion

    #region IsDefined
    /// <summary>
    ///  Indicates whether custom attributes of a specified type are applied to a specified assembly.
    /// </summary>
    /// <param name="element">The assembly to inspect.</param>
    /// <param name="attributeType">The type of attribute to search for.</param>
    /// <returns><see langword="true"/> if an attribute of the specified type is applied to <paramref name="element"/>; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="element"/> or <paramref name="attributeType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="attributeType"/> is not derived from <see cref="Attribute"/>.</exception>
    public static bool IsDefined(this Assembly element, Type attributeType)
    {
        return Attribute.IsDefined(element, attributeType);
    }

    /// <inheritdoc cref="IsDefined(Assembly, Type)"/>
    /// <summary>
    ///  Indicates whether custom attributes of a specified type are applied to a specified module.
    /// </summary>
    /// <param name="element">The module to inspect.</param>
    public static bool IsDefined(this Module element, Type attributeType)
    {
        return Attribute.IsDefined(element, attributeType);
    }

    /// <inheritdoc cref="IsDefined(Assembly, Type)"/>
    /// <summary>
    ///  Indicates whether custom attributes of a specified type are applied to a specified member.
    /// </summary>
    /// <param name="element">The member to inspect.</param>
    /// <exception cref="NotSupportedException"><paramref name="element"/> is not a constructor, method, property, event, type, or field.</exception>
    public static bool IsDefined(this MemberInfo element, Type attributeType)
    {
        return Attribute.IsDefined(element, attributeType);
    }

    /// <inheritdoc cref="IsDefined(Assembly, Type)"/>
    /// <summary>
    ///  Indicates whether custom attributes of a specified type are applied to a specified parameter.
    /// </summary>
    /// <param name="element">The parameter to inspect.</param>
    public static bool IsDefined(this ParameterInfo element, Type attributeType)
    {
        return Attribute.IsDefined(element, attributeType);
    }

    /// <inheritdoc cref="IsDefined(MemberInfo, Type)"/>
    /// <param name="inherit"><see langword="true"/> to inspect the ancestors of <paramref name="element"/>; otherwise, <see langword="false"/>.</param>
    public static bool IsDefined(this MemberInfo element, Type attributeType, bool inherit)
    {
        return Attribute.IsDefined(element, attributeType, inherit);
    }

    /// <inheritdoc cref="IsDefined(ParameterInfo, Type)"/>
    /// <param name="inherit"><see langword="true"/> to inspect the ancestors of <paramref name="element"/>; otherwise, <see langword="false"/>.</param>
    public static bool IsDefined(this ParameterInfo element, Type attributeType, bool inherit)
    {
        return Attribute.IsDefined(element, attributeType, inherit);
    }
    #endregion
}
