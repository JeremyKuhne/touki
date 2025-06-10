// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class SimpleServiceProviderTests
{
    // Test interfaces and classes for service registration
    private interface ITestService { }
    private interface IExtendedService : ITestService { int Value { get; } }

    private class TestService : ITestService { }
    private class ExtendedService : IExtendedService
    {
        public int Value => 42;
    }

    [Fact]
    public void AddService_RegistersServiceByExactType()
    {
        SimpleServiceProvider provider = new();
        TestService service = new();

        provider.AddService(service);

        object? result = provider.GetService(typeof(TestService));
        result.Should().BeSameAs(service);
    }

    [Fact]
    public void AddService_OverwritesPreviousRegistration()
    {
        SimpleServiceProvider provider = new();
        TestService service1 = new();
        TestService service2 = new();

        provider.AddService(service1);
        provider.AddService(service2);

        object? result = provider.GetService(typeof(TestService));
        result.Should().BeSameAs(service2);
        result.Should().NotBeSameAs(service1);
    }

    [Fact]
    public void GetService_WithType_ReturnsNullForUnregisteredService()
    {
        SimpleServiceProvider provider = new();

        object? result = provider.GetService(typeof(TestService));

        result.Should().BeNull();
    }

    [Fact]
    public void GetService_WithType_ReturnsRegisteredService()
    {
        SimpleServiceProvider provider = new();
        TestService service = new();

        provider.AddService(service);

        object? result = provider.GetService(typeof(TestService));
        result.Should().BeSameAs(service);
    }

    [Fact]
    public void GetService_Generic_ReturnsNullForUnregisteredService()
    {
        SimpleServiceProvider provider = new();

        TestService? result = provider.GetService<TestService>();

        result.Should().BeNull();
    }

    [Fact]
    public void GetService_Generic_ReturnsRegisteredService()
    {
        SimpleServiceProvider provider = new();
        TestService service = new();

        provider.AddService(service);

        TestService? result = provider.GetService<TestService>();
        result.Should().BeSameAs(service);
    }

    [Fact]
    public void TryGetService_ReturnsFalseForUnregisteredService()
    {
        SimpleServiceProvider provider = new();

        bool success = provider.TryGetService(out TestService? result);

        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryGetService_ReturnsTrueAndServiceForRegisteredService()
    {
        SimpleServiceProvider provider = new();
        TestService service = new();

        provider.AddService(service);

        bool success = provider.TryGetService(out TestService? result);

        success.Should().BeTrue();
        result.Should().BeSameAs(service);
    }

    [Fact]
    public void GetService_DoesNotRetrieveByInterface()
    {
        SimpleServiceProvider provider = new();
        TestService service = new();

        provider.AddService(service);

        ITestService? result = provider.GetService<ITestService>();
        result.Should().BeNull();
    }

    [Fact]
    public void AddService_CanRegisterImplementationsByInterface()
    {
        SimpleServiceProvider provider = new();
        ITestService service = new TestService();

        provider.AddService(service);

        ITestService? result = provider.GetService<ITestService>();
        result.Should().BeSameAs(service);
    }

    [Fact]
    public void GetService_InterfaceHierarchy_RequiresExactRegistration()
    {
        SimpleServiceProvider provider = new();
        IExtendedService extendedService = new ExtendedService();

        provider.AddService(extendedService);

        // Should return the service when requested by its registered interface
        IExtendedService? result1 = provider.GetService<IExtendedService>();
        result1.Should().BeSameAs(extendedService);

        // Should not return the service when requested by a base interface
        ITestService? result2 = provider.GetService<ITestService>();
        result2.Should().BeNull();
    }

    [Fact]
    public void AddService_MultipleDifferentServices()
    {
        SimpleServiceProvider provider = new();
        TestService testService = new();
        ExtendedService extendedService = new();

        provider.AddService(testService);
        provider.AddService(extendedService);

        TestService? result1 = provider.GetService<TestService>();
        ExtendedService? result2 = provider.GetService<ExtendedService>();

        result1.Should().BeSameAs(testService);
        result2.Should().BeSameAs(extendedService);
    }

    [Fact]
    public void AddService_NullService_StoresNullReference()
    {
        SimpleServiceProvider provider = new();
        TestService? nullService = null;

        // This would be prevented by the compiler due to the constraint T : class
        // But we can simulate it by using null! to satisfy the compiler
        provider.AddService(nullService!);

        // When retrieving, we should get null back
        TestService? result = provider.GetService<TestService>();
        result.Should().BeNull();

        // TryGetService should return false
        bool success = provider.TryGetService(out TestService? outResult);
        success.Should().BeFalse();
        outResult.Should().BeNull();
    }

    [Fact]
    public async Task MultipleThreads_CanAddAndRetrieveServices()
    {
        SimpleServiceProvider provider = new();
        int threadCount = 10;
        int itemsPerThread = 100;

        List<Task> tasks = new(threadCount);

        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            tasks.Add(Task.Run(async () =>
            {
                // Each thread adds a range of services
                for (int j = 0; j < itemsPerThread; j++)
                {
                    int serviceId = (threadId * itemsPerThread) + j;
                    CustomService service = new(serviceId);
                    provider.AddService(service);

                    // Immediately verify it can be retrieved
                    CustomService? retrieved = provider.GetService<CustomService>();
                    retrieved.Should().NotBeNull();

                    // Small delay to simulate real work and increase the chance of thread interleaving
                    await Task.Delay(1).ConfigureAwait(continueOnCapturedContext: false);
                }
            }));
        }

        // Wait for all tasks to complete asynchronously
        await Task.WhenAll(tasks);

        // The last added service should be available
        CustomService? lastService = provider.GetService<CustomService>();
        lastService.Should().NotBeNull();
    }

    // Custom service class for threading test
    private class CustomService
    {
        public int Id { get; }

        public CustomService(int id) => Id = id;
    }
}
