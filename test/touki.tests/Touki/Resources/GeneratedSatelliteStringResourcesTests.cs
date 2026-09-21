// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;

namespace Touki.Resources;

[TestClass]
[DoNotParallelize]
public class GeneratedSatelliteStringResourcesTests
{
    [TestMethod]
    public void ResourceManager_LocalizedSiblingExists_UsesSatelliteManager()
    {
        GeneratedSatelliteTestStrings.ResourceManager.Should().BeOfType<SatelliteStringResourceManager>();
    }

    [TestMethod]
    public void Greeting_ExactCulture_ReturnsLocalizedValue()
    {
        try
        {
            GeneratedSatelliteTestStrings.Culture = CultureInfo.GetCultureInfo("de");

            GeneratedSatelliteTestStrings.Greeting.Should().Be("Hallo");
        }
        finally
        {
            GeneratedSatelliteTestStrings.Culture = null;
        }
    }

    [TestMethod]
    public void Greeting_ParentCulture_ReturnsParentValue()
    {
        try
        {
            GeneratedSatelliteTestStrings.Culture = CultureInfo.GetCultureInfo("de-DE");

            GeneratedSatelliteTestStrings.Greeting.Should().Be("Hallo");
        }
        finally
        {
            GeneratedSatelliteTestStrings.Culture = null;
        }
    }

    [TestMethod]
    public void Greeting_MissingCulture_ReturnsNeutralValue()
    {
        try
        {
            GeneratedSatelliteTestStrings.Culture = CultureInfo.GetCultureInfo("fr");

            GeneratedSatelliteTestStrings.Greeting.Should().Be("Hello");
        }
        finally
        {
            GeneratedSatelliteTestStrings.Culture = null;
        }
    }

    [TestMethod]
    public void Culture_AfterCachedRead_ReplacesCachedValues()
    {
        try
        {
            GeneratedSatelliteTestStrings.Culture = CultureInfo.InvariantCulture;
            GeneratedSatelliteTestStrings.Greeting.Should().Be("Hello");

            GeneratedSatelliteTestStrings.Culture = CultureInfo.GetCultureInfo("de");

            GeneratedSatelliteTestStrings.Greeting.Should().Be("Hallo");
        }
        finally
        {
            GeneratedSatelliteTestStrings.Culture = null;
        }
    }

    [TestMethod]
    public void Culture_NullAndCurrentUICultureChanges_KeepsValueUntilReset()
    {
        CultureInfo originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            GeneratedSatelliteTestStrings.Culture = null;
            string neutral = GeneratedSatelliteTestStrings.Greeting;

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");

            GeneratedSatelliteTestStrings.Greeting.Should().BeSameAs(neutral);
            GeneratedSatelliteTestStrings.Culture = null;
            GeneratedSatelliteTestStrings.Greeting.Should().Be("Hallo");
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
            GeneratedSatelliteTestStrings.Culture = null;
        }
    }

    [TestMethod]
    public void ReleaseAllResources_CachedProperty_RemainsCached()
    {
        try
        {
            GeneratedSatelliteTestStrings.Culture = CultureInfo.InvariantCulture;
            string value = GeneratedSatelliteTestStrings.Greeting;
            object cache = typeof(GeneratedSatelliteTestStrings).TestAccessor.Dynamic.s_cache;

            GeneratedSatelliteTestStrings.ResourceManager.ReleaseAllResources();

            ((object)typeof(GeneratedSatelliteTestStrings).TestAccessor.Dynamic.s_cache).Should().BeSameAs(cache);
            GeneratedSatelliteTestStrings.Greeting.Should().BeSameAs(value);
        }
        finally
        {
            GeneratedSatelliteTestStrings.Culture = null;
        }
    }
}