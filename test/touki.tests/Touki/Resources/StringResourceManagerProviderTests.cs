// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Reflection;

namespace Touki.Resources;

[TestClass]
[DoNotParallelize]
public class StringResourceManagerProviderTests
{
    private static readonly Assembly s_assembly = typeof(StringResourceManagerProviderTests).Assembly;

    [TestInitialize]
    public void ResetBeforeTest() => StringResourceManagerProvider.ResetForTests();

    [TestCleanup]
    public void ResetAfterTest() => StringResourceManagerProvider.ResetForTests();

    [TestMethod]
    public void Create_WithoutRegistration_ReturnsRuntimeSatelliteManager()
    {
        StringResourceManager manager = StringResourceManagerProvider.Create("Test.Resources", s_assembly);

        manager.Should().BeOfType<SatelliteStringResourceManager>();
    }

    [TestMethod]
    public void Create_RegisteredProvider_ReturnsProviderManagerAndPassesInputs()
    {
        StringResourceManager expected = new("Expected", s_assembly);
        string? receivedBaseName = null;
        Assembly? receivedAssembly = null;
        StringResourceManagerProvider.Register(
            (baseName, ownerAssembly) =>
            {
                receivedBaseName = baseName;
                receivedAssembly = ownerAssembly;
                return expected;
            });

        StringResourceManager actual = StringResourceManagerProvider.Create("Test.Resources", s_assembly);

        actual.Should().BeSameAs(expected);
        receivedBaseName.Should().Be("Test.Resources");
        receivedAssembly.Should().BeSameAs(s_assembly);
    }

    [TestMethod]
    public void Create_EmbeddedProvider_ReturnsEmbeddedManager()
    {
        StringResourceManagerProvider.RegisterEmbedded();

        StringResourceManager manager = StringResourceManagerProvider.Create("Test.Resources", s_assembly);

        manager.Should().BeOfType<ResourceManagerAdapter>();
    }

    [TestMethod]
    public void Register_ProviderAlreadyRegistered_ThrowsInvalidOperationException()
    {
        StringResourceManagerProvider.Register(
            (baseName, ownerAssembly) => new StringResourceManager(baseName, ownerAssembly));

        Action action = () => StringResourceManagerProvider.Register(
            (baseName, ownerAssembly) => new StringResourceManager(baseName, ownerAssembly));

        action.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void Register_DefaultAlreadySelected_ThrowsInvalidOperationException()
    {
        _ = StringResourceManagerProvider.Create("Test.Resources", s_assembly);

        Action action = () => StringResourceManagerProvider.Register(
            (baseName, ownerAssembly) => new StringResourceManager(baseName, ownerAssembly));

        action.Should().Throw<InvalidOperationException>();
    }
}
