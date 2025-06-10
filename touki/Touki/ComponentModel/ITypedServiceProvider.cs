// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  Typed version of <see cref="IServiceProvider"/> that allows retrieving services by type.
/// </summary>
public interface ITypedServiceProvider : IServiceProvider
{
    /// <summary>
    ///  Gets a service of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of service to get.</typeparam>
    /// <returns>The service instance, or <see langword="null"/> if not found.</returns>
    T? GetService<T>() where T : class;

    /// <summary>
    ///  Attempts to get a service of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of service to get.</typeparam>
    /// <param name="service">When this method returns, contains the service instance, or <see langword="null"/> if not found.</param>
    /// <returns><see langword="true"/> if the service was found; otherwise, <see langword="false"/>.</returns>
    bool TryGetService<T>([NotNullWhen(true)] out T? service) where T : class;
}
