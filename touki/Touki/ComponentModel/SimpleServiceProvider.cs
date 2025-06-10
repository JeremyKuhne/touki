// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Concurrent;

namespace Touki;

/// <summary>
///  A simple implementation of <see cref="ITypedServiceProvider"/>.
/// </summary>
public class SimpleServiceProvider : ITypedServiceProvider
{
    private readonly ConcurrentDictionary<Type, object> _services = new();

    /// <summary>
    ///  Adds a service of the specified type to the provider.
    /// </summary>
    /// <typeparam name="T">The type of service to add.</typeparam>
    /// <param name="service">The service instance to add.</param>
    public void AddService<T>(T service) where T : class
    {
        _services[typeof(T)] = service;
    }

    /// <inheritdoc cref="IServiceProvider.GetService(Type)"/>/>
    public object? GetService(Type serviceType)
    {
        _services.TryGetValue(serviceType, out object? service);
        return service;
    }

    /// <inheritdoc cref="ITypedServiceProvider.GetService{T}"/>/>
    public T? GetService<T>() where T : class => GetService(typeof(T)) as T;

    /// <inheritdoc cref="ITypedServiceProvider.TryGetService{T}"/>/>
    public bool TryGetService<T>([NotNullWhen(true)] out T? service) where T : class
    {
        service = default;

        if (_services.TryGetValue(typeof(T), out object? value))
        {
            service = value as T;
        }

        return service is not null;
    }
}
