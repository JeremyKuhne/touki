// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using System.Reflection;
using System.Resources;

namespace Touki.Resources.Generator.Tests;

[TestClass]
[DoNotParallelize]
public class GeneratedResourceRuntimeTests
{
    private const string SimpleResource = """
        <root>
          <data name="Greeting" xml:space="preserve">
            <value>Source fallback</value>
          </data>
        </root>
        """;

    [TestInitialize]
    public void ResetProviderBeforeTest() => StringResourceManagerProvider.ResetForTests();

    [TestCleanup]
    public void ResetProviderAfterTest() => StringResourceManagerProvider.ResetForTests();

    [TestMethod]
    public void ResourceManager_LocalizedResourceWithoutRegistration_UsesRuntimeSatellites()
    {
        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource),
            GeneratorTestResource.Sibling(SimpleResource, "fr"));

        using GeneratedAssembly generatedAssembly = result.Emit(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Greeting"] = "Runtime value"
            });

        object? manager = GetRequiredProperty(
            generatedAssembly.GetGeneratedType(),
            "ResourceManager").GetValue(obj: null);

        manager.Should().BeOfType<SatelliteStringResourceManager>();
    }

    [TestMethod]
    public async Task ResourceManager_RegisteredProvider_InvokesProviderOnce()
    {
        int invocationCount = 0;
        string? receivedBaseName = null;
        Assembly? receivedAssembly = null;
        StringResourceManagerProvider.Register(
            (baseName, ownerAssembly) =>
            {
                Interlocked.Increment(ref invocationCount);
                receivedBaseName = baseName;
                receivedAssembly = ownerAssembly;
                return new StringResourceManager(baseName, ownerAssembly);
            });

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource),
            GeneratorTestResource.Sibling(SimpleResource, "fr"));

        using GeneratedAssembly generatedAssembly = result.Emit(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Greeting"] = "Runtime value"
            });

        PropertyInfo resourceManager = GetRequiredProperty(
            generatedAssembly.GetGeneratedType(),
            "ResourceManager");

        Task<object?>[] reads =
        [
            .. Enumerable.Range(0, 16)
                .Select(_ => Task.Run(() => resourceManager.GetValue(obj: null)))
        ];

        object?[] managers = await Task.WhenAll(reads).ConfigureAwait(continueOnCapturedContext: false);

        invocationCount.Should().Be(1);
        managers.Should().OnlyContain(manager => ReferenceEquals(manager, managers[0]));
        managers[0].Should().BeOfType<StringResourceManager>();
        receivedBaseName.Should().Be("Test.Resources.Strings");
        receivedAssembly.Should().BeSameAs(generatedAssembly.Assembly);
    }

    [TestMethod]
    public void ResourceManager_NeutralOnlyWithProviderOptIn_InvokesProviderOnce()
    {
        int invocationCount = 0;
        StringResourceManagerProvider.Register(
            (baseName, ownerAssembly) =>
            {
                invocationCount++;
                return new StringResourceManager(baseName, ownerAssembly);
            });

        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["UseResourceManagerProvider"] = "true"
        };

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource, metadata: metadata));

        using GeneratedAssembly generatedAssembly = result.Emit(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Greeting"] = "Runtime value"
            });

        Type resourceType = generatedAssembly.GetGeneratedType();
        PropertyInfo manager = GetRequiredProperty(resourceType, "ResourceManager");

        manager.GetValue(obj: null).Should().BeOfType<StringResourceManager>();
        GetRequiredProperty(resourceType, "Greeting").GetValue(obj: null).Should().Be("Runtime value");
        invocationCount.Should().Be(1);
    }

    [TestMethod]
    public void GeneratedProperty_RepeatRead_CachesValue()
    {
        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource));

        using GeneratedAssembly generatedAssembly = result.Emit(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Greeting"] = "Runtime value"
            });

        Type resourceType = generatedAssembly.GetGeneratedType();
        PropertyInfo greeting = GetRequiredProperty(resourceType, "Greeting");

        object? first = greeting.GetValue(obj: null);
        object? second = greeting.GetValue(obj: null);

        first.Should().Be("Runtime value");
        second.Should().BeSameAs(first);
    }

    [TestMethod]
    public void Culture_Set_ReplacesWholeCache()
    {
        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource));

        using GeneratedAssembly generatedAssembly = result.Emit(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Greeting"] = "Runtime value"
            });

        Type resourceType = generatedAssembly.GetGeneratedType();
        FieldInfo cacheField = GetRequiredField(resourceType, "s_cache");
        PropertyInfo cultureProperty = GetRequiredProperty(resourceType, "Culture");
        object? originalCache = cacheField.GetValue(obj: null);
        CultureInfo culture = new("fr-FR");

        cultureProperty.SetValue(obj: null, culture);

        object? replacementCache = cacheField.GetValue(obj: null);
        replacementCache.Should().NotBeSameAs(originalCache);
        cultureProperty.GetValue(obj: null).Should().BeSameAs(culture);
    }

    [TestMethod]
    public void GeneratedProperty_MissingRequiredResource_Throws()
    {
        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource));

        using GeneratedAssembly generatedAssembly = result.Emit(
            new Dictionary<string, string>(StringComparer.Ordinal));

        PropertyInfo greeting = GetRequiredProperty(generatedAssembly.GetGeneratedType(), "Greeting");

        Action action = () => greeting.GetValue(obj: null);

        action.Should().Throw<TargetInvocationException>()
            .Which.InnerException.Should().BeOfType<MissingManifestResourceException>();
    }

    [TestMethod]
    public void GeneratedProperty_MissingResourceWithDefault_ReturnsAndCachesDefault()
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["IncludeDefaultValues"] = "true"
        };

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource, metadata: metadata));

        using GeneratedAssembly generatedAssembly = result.Emit(
            new Dictionary<string, string>(StringComparer.Ordinal));

        PropertyInfo greeting = GetRequiredProperty(generatedAssembly.GetGeneratedType(), "Greeting");

        object? first = greeting.GetValue(obj: null);
        object? second = greeting.GetValue(obj: null);

        first.Should().Be("Source fallback");
        second.Should().BeSameAs(first);
    }

    [TestMethod]
    public void FormatMethod_NamedPlaceholder_FormatsCachedProperty()
    {
        const string resource = """
            <root>
              <data name="Greeting"><value>Hello {name}</value></data>
            </root>
            """;

        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["EmitFormatMethods"] = "true"
        };

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(resource, metadata: metadata));

        using GeneratedAssembly generatedAssembly = result.Emit(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Greeting"] = "Hello {name}"
            });

        Type resourceType = generatedAssembly.GetGeneratedType();
        MethodInfo formatGreeting = resourceType.GetMethod(
            "FormatGreeting",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("FormatGreeting was not found.");

        object? formatted = formatGreeting.Invoke(obj: null, ["Ada"]);

        formatted.Should().Be("Hello Ada");
        object? cachedValue = GetRequiredProperty(resourceType, "Greeting").GetValue(obj: null);
        cachedValue.Should().Be("Hello {name}");
    }

    [TestMethod]
    public void FormatMethod_SparseNumericPlaceholder_PadsParameters()
    {
        const string resource = """
            <root>
              <data name="Greeting"><value>Hello {1}</value></data>
            </root>
            """;

        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["EmitFormatMethods"] = "true"
        };

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(resource, metadata: metadata));

        using GeneratedAssembly generatedAssembly = result.Emit(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Greeting"] = "Hello {1}"
            });

        Type resourceType = generatedAssembly.GetGeneratedType();
        MethodInfo formatGreeting = resourceType.GetMethod(
            "FormatGreeting",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("FormatGreeting was not found.");

        object? formatted = formatGreeting.Invoke(obj: null, ["unused", "Ada"]);

        formatted.Should().Be("Hello Ada");
    }

    private static PropertyInfo GetRequiredProperty(Type type, string name)
    {
        return type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Property '{name}' was not found.");
    }

    private static FieldInfo GetRequiredField(Type type, string name)
    {
        return type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Field '{name}' was not found.");
    }
}