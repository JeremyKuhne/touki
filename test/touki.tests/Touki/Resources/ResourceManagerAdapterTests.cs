// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using System.Reflection;

namespace Touki.Resources;

[TestClass]
public class ResourceManagerAdapterTests
{
    private static readonly Assembly s_assembly = typeof(ResourceManagerAdapterTests).Assembly;

    private static string NeutralBaseName()
    {
        string resourceName = s_assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("SatelliteTestStrings.resources", StringComparison.Ordinal));

        return resourceName[..^".resources".Length];
    }

    [TestMethod]
    public void GetString_ExactCulture_ReturnsLocalizedValue()
    {
        ResourceManagerAdapter manager = new(NeutralBaseName(), s_assembly);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_ParentCulture_ReturnsParentValue()
    {
        ResourceManagerAdapter manager = new(NeutralBaseName(), s_assembly);

        manager.GetString("Greeting", new CultureInfo("de-DE")).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_MissingCulture_ReturnsNeutralValue()
    {
        ResourceManagerAdapter manager = new(NeutralBaseName(), s_assembly);

        manager.GetString("Greeting", new CultureInfo("fr-FR")).Should().Be("Hello");
    }

    [TestMethod]
    public void ReleaseAllResources_AfterLocalizedLookup_AllowsReload()
    {
        ResourceManagerAdapter manager = new(NeutralBaseName(), s_assembly);
        CultureInfo german = new("de");

        manager.GetString("Greeting", german).Should().Be("Hallo");
        manager.ReleaseAllResources();

        manager.GetString("Greeting", german).Should().Be("Hallo");
    }
}
