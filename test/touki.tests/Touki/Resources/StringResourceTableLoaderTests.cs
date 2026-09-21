// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Resources;
using System.Resources.Extensions;
using System.Text;

namespace Touki.Resources;

[TestClass]
public class StringResourceTableLoaderTests
{
    private static readonly Assembly s_assembly = typeof(StringResourceTableLoaderTests).Assembly;

    private sealed class DisposalTracker : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class ResourceAssembly(byte[] resource) : Assembly
    {
        public override Stream? GetManifestResourceStream(string name) => new MemoryStream(resource, writable: false);
    }

    private static byte[] WriteResources(Action<ResourceWriter> write)
    {
        using MemoryStream stream = new();
        using (ResourceWriter writer = new(stream))
        {
            write(writer);
            writer.Generate();
        }

        return stream.ToArray();
    }

    private static string NeutralResourceName() => s_assembly.GetManifestResourceNames()
        .Single(name => name.EndsWith("SatelliteTestStrings.resources", StringComparison.Ordinal));

    [TestMethod]
    public void LoadIndexedTableFromAssembly_LoadedAssembly_ReturnsIndexedTable()
    {
        using IndexedStringResourceTable table = StringResourceTableLoader.LoadIndexedTableFromAssembly(
            s_assembly,
            NeutralResourceName(),
            StringResourceManagerOptions.None)
            ?? throw new InvalidOperationException("Expected the embedded resource.");

        table.Lookup("Greeting", out string? value).Should().Be(StringResourceLookupKind.Found);
        value.Should().Be("Hello");
    }

    [TestMethod]
    public void LoadIndexedTableFromAssembly_LoadedAssemblyResourceIsMissing_ReturnsNull()
    {
        IndexedStringResourceTable? table = StringResourceTableLoader.LoadIndexedTableFromAssembly(
            s_assembly,
            "Missing.resources",
            StringResourceManagerOptions.None);

        table.Should().BeNull();
    }

    [TestMethod]
    public void LoadIndexedTableFromAssembly_LoadedAssemblyBadMagic_ThrowsBadImageFormatException()
    {
        Assembly assembly = new ResourceAssembly(new byte[64]);

        Action action = () => StringResourceTableLoader.LoadIndexedTableFromAssembly(
            assembly,
            "Strings.resources",
            StringResourceManagerOptions.None);

        action.Should().Throw<BadImageFormatException>()
            .Which.InnerException.Should().BeOfType<ArgumentException>();
    }

    [TestMethod]
    public void LoadIndexedTableFromAssembly_ImageTransfersOwnerUntilTableIsDisposed()
    {
        byte[] assembly = System.IO.File.ReadAllBytes(s_assembly.Location);
        DisposalTracker owner = new();
        try
        {
            IndexedStringResourceTable table = StringResourceTableLoader.LoadIndexedTableFromAssembly(
                assembly,
                NeutralResourceName(),
                StringResourceManagerOptions.None,
                owner)
                ?? throw new InvalidOperationException("Expected the embedded resource.");
            try
            {
                owner.IsDisposed.Should().BeFalse();
                table.Lookup("Greeting", out string? value).Should().Be(StringResourceLookupKind.Found);
                value.Should().Be("Hello");
            }
            finally
            {
                table.Dispose();
            }

            owner.IsDisposed.Should().BeTrue();
        }
        finally
        {
            owner.Dispose();
        }
    }

    [TestMethod]
    public void LoadIndexedTableFromAssembly_ImageResourceIsMissing_DisposesOwner()
    {
        byte[] assembly = System.IO.File.ReadAllBytes(s_assembly.Location);
        DisposalTracker owner = new();
        try
        {
            IndexedStringResourceTable? table = StringResourceTableLoader.LoadIndexedTableFromAssembly(
                assembly,
                "Missing.resources",
                StringResourceManagerOptions.None,
                owner);

            table.Should().BeNull();
            owner.IsDisposed.Should().BeTrue();
        }
        finally
        {
            owner.Dispose();
        }
    }

    [TestMethod]
    public void LoadIndexedTableFromAssembly_InvalidOptions_DisposesOwner()
    {
        DisposalTracker owner = new();
        try
        {
            Action action = () => StringResourceTableLoader.LoadIndexedTableFromAssembly(
                ReadOnlyMemory<byte>.Empty,
                "Missing.resources",
                (StringResourceManagerOptions)int.MaxValue,
                owner);

            action.Should().Throw<ArgumentOutOfRangeException>();
            owner.IsDisposed.Should().BeTrue();
        }
        finally
        {
            owner.Dispose();
        }
    }

    [TestMethod]
    public void LoadIndexedTableFromAssembly_EmptyImage_DisposesOwner()
    {
        DisposalTracker owner = new();
        try
        {
            Action action = () => StringResourceTableLoader.LoadIndexedTableFromAssembly(
                ReadOnlyMemory<byte>.Empty,
                "Missing.resources",
                StringResourceManagerOptions.None,
                owner);

            action.Should().Throw<BadImageFormatException>();
            owner.IsDisposed.Should().BeTrue();
        }
        finally
        {
            owner.Dispose();
        }
    }

    [TestMethod]
    public void LoadIndexedTableFromResourcesFile_TransfersOwnerUntilTableIsDisposed()
    {
        byte[] resources = WriteResources(static writer => writer.AddResource("Greeting", "Hello"));
        DisposalTracker owner = new();
        try
        {
            IndexedStringResourceTable table = StringResourceTableLoader.LoadIndexedTableFromResourcesFile(
                resources,
                StringResourceManagerOptions.None,
                owner);
            try
            {
                owner.IsDisposed.Should().BeFalse();
                table.Lookup("Greeting", out string? value).Should().Be(StringResourceLookupKind.Found);
                value.Should().Be("Hello");
            }
            finally
            {
                table.Dispose();
            }

            owner.IsDisposed.Should().BeTrue();
        }
        finally
        {
            owner.Dispose();
        }
    }

    [TestMethod]
    public void LoadIndexedTableFromResourcesFile_InvalidOptions_DisposesOwner()
    {
        byte[] resources = WriteResources(static writer => writer.AddResource("Greeting", "Hello"));
        DisposalTracker owner = new();
        try
        {
            Action action = () => StringResourceTableLoader.LoadIndexedTableFromResourcesFile(
                resources,
                (StringResourceManagerOptions)int.MaxValue,
                owner);

            action.Should().Throw<ArgumentOutOfRangeException>();
            owner.IsDisposed.Should().BeTrue();
        }
        finally
        {
            owner.Dispose();
        }
    }

    [TestMethod]
    public void LoadIndexedTableFromResourcesStream_TransfersStreamUntilTableIsDisposed()
    {
        byte[] resources = WriteResources(static writer => writer.AddResource("Greeting", "Hello"));
        using MemoryStream stream = new(resources, writable: false);
        IndexedStringResourceTable table = StringResourceTableLoader.LoadIndexedTableFromResourcesStream(
            stream,
            StringResourceManagerOptions.None);
        try
        {
            stream.CanRead.Should().BeTrue();
            table.Lookup("Greeting", out string? value).Should().Be(StringResourceLookupKind.Found);
            value.Should().Be("Hello");
        }
        finally
        {
            table.Dispose();
        }

        stream.CanRead.Should().BeFalse();
    }

    [TestMethod]
    public void LoadIndexedTableFromResourcesStream_InvalidOptions_DisposesStream()
    {
        byte[] resources = WriteResources(static writer => writer.AddResource("Greeting", "Hello"));
        using MemoryStream stream = new(resources, writable: false);

        Action action = () => StringResourceTableLoader.LoadIndexedTableFromResourcesStream(
            stream,
            (StringResourceManagerOptions)int.MaxValue);

        action.Should().Throw<ArgumentOutOfRangeException>();
        stream.CanRead.Should().BeFalse();
    }

    [TestMethod]
    public void LoadStringTableFromAssembly_EmbeddedResource_ReturnsStrings()
    {
        byte[] assembly = System.IO.File.ReadAllBytes(s_assembly.Location);

        Dictionary<string, string>? table = StringResourceTableLoader.LoadStringTableFromAssembly(
            assembly,
            NeutralResourceName());

        table.Should().NotBeNull();
        table["Greeting"].Should().Be("Hello");
        table["Farewell"].Should().Be("Goodbye");
        table.Comparer.Should().BeSameAs(StringComparer.Ordinal);
    }

    [TestMethod]
    public void LoadStringTableFromAssembly_LoadedAssembly_ReturnsStrings()
    {
        Dictionary<string, string>? table = StringResourceTableLoader.LoadStringTableFromAssembly(
            s_assembly,
            NeutralResourceName());

        table.Should().NotBeNull();
        table["Greeting"].Should().Be("Hello");
        table["Farewell"].Should().Be("Goodbye");
        table.Comparer.Should().BeSameAs(StringComparer.Ordinal);
    }

    [TestMethod]
    public void LoadStringTableFromAssembly_ResourceIsMissing_ReturnsNull()
    {
        byte[] assembly = System.IO.File.ReadAllBytes(s_assembly.Location);

        Dictionary<string, string>? table = StringResourceTableLoader.LoadStringTableFromAssembly(
            assembly,
            "Missing.resources");

        table.Should().BeNull();
    }

    [TestMethod]
    public void LoadStringTableFromAssembly_LoadedAssemblyInvalidOptions_ThrowsArgumentOutOfRangeException()
    {
        Action action = () => StringResourceTableLoader.LoadStringTableFromAssembly(
            s_assembly,
            "Missing.resources",
            (StringResourceManagerOptions)int.MaxValue);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void LoadStringTableFromAssembly_ImageInvalidOptions_ThrowsArgumentOutOfRangeException()
    {
        Action action = () => StringResourceTableLoader.LoadStringTableFromAssembly(
            ReadOnlyMemory<byte>.Empty,
            "Missing.resources",
            (StringResourceManagerOptions)int.MaxValue);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void LoadStringTableFromAssembly_LoadedAssemblyBadMagic_ThrowsBadImageFormatException()
    {
        Assembly assembly = new ResourceAssembly(new byte[64]);

        Action action = () => StringResourceTableLoader.LoadStringTableFromAssembly(
            assembly,
            "Strings.resources");

        action.Should().Throw<BadImageFormatException>()
            .Which.InnerException.Should().BeOfType<ArgumentException>();
    }

    [TestMethod]
    public void LoadStringTableFromAssembly_LoadedAssemblyResourceIsMissing_ReturnsNull()
    {
        Dictionary<string, string>? table = StringResourceTableLoader.LoadStringTableFromAssembly(
            s_assembly,
            "Missing.resources");

        table.Should().BeNull();
    }

    [TestMethod]
    public void LoadStringTableFromAssembly_MemorySlice_ReturnsStrings()
    {
        byte[] assembly = System.IO.File.ReadAllBytes(s_assembly.Location);
        byte[] padded = new byte[assembly.Length + 16];
        assembly.CopyTo(padded, 7);
        ReadOnlyMemory<byte> image = new(padded, 7, assembly.Length);

        Dictionary<string, string>? table = StringResourceTableLoader.LoadStringTableFromAssembly(
            image,
            NeutralResourceName());

        table.Should().NotBeNull();
        table["Greeting"].Should().Be("Hello");
    }

    [TestMethod]
    public void LoadStringTableFromAssembly_EmptyImage_ThrowsBadImageFormatException()
    {
        Action action = () => StringResourceTableLoader.LoadStringTableFromAssembly(
            ReadOnlyMemory<byte>.Empty,
            "Strings.resources");

        action.Should().Throw<BadImageFormatException>();
    }

    [TestMethod]
    public void LoadStringTableFromAssembly_EmbeddedResourceHasBadMagic_ThrowsBadImageFormatException()
    {
        byte[] assembly = System.IO.File.ReadAllBytes(s_assembly.Location);
        int contentOffset = GetEmbeddedResourceContentOffset(assembly, NeutralResourceName());
        assembly.AsSpan(contentOffset, sizeof(int)).Clear();

        Action action = () => StringResourceTableLoader.LoadStringTableFromAssembly(
            assembly,
            NeutralResourceName());

        action.Should().Throw<BadImageFormatException>()
            .Which.InnerException.Should().BeOfType<ArgumentException>();
    }

    [TestMethod]
    public void LoadStringTableFromResourcesFile_StringsAndNonStrings_ThrowsNotSupportedException()
    {
        byte[] resources = WriteResources(static writer =>
        {
            writer.AddResource("Greeting", "Hello");
            writer.AddResource("Count", 42);
        });

        Action action = () => StringResourceTableLoader.LoadStringTableFromResourcesFile(resources);

        action.Should().Throw<NotSupportedException>();
    }

    [TestMethod]
    public void LoadStringTableFromResourcesFile_IgnoreNonStrings_ReturnsOnlyStrings()
    {
        byte[] resources = WriteResources(static writer =>
        {
            writer.AddResource("Greeting", "Hello");
            writer.AddResource("Count", 42);
        });

        Dictionary<string, string> table = StringResourceTableLoader.LoadStringTableFromResourcesFile(
            resources,
            StringResourceManagerOptions.IgnoreNonStringResources);

        table.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, string>("Greeting", "Hello"));
        table.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        table.ContainsKey("greeting").Should().BeFalse();
    }

    [TestMethod]
    public void LoadStringTableFromResourcesFile_NullResource_ThrowsBadImageFormatException()
    {
        byte[] resources = WriteResources(static writer => writer.AddResource("Null", (object?)null));

        Action strict = () => StringResourceTableLoader.LoadStringTableFromResourcesFile(resources);
        Action ignore = () => StringResourceTableLoader.LoadStringTableFromResourcesFile(
            resources,
            StringResourceManagerOptions.IgnoreNonStringResources);

        strict.Should().Throw<BadImageFormatException>();
        ignore.Should().Throw<BadImageFormatException>();
    }

    [TestMethod]
    public void LoadStringTableFromResourcesFile_EmptyTable_ReturnsEmptyDictionary()
    {
        byte[] resources = WriteResources(static _ => { });

        Dictionary<string, string> table = StringResourceTableLoader.LoadStringTableFromResourcesFile(resources);

        table.Should().BeEmpty();
        table.Comparer.Should().BeSameAs(StringComparer.Ordinal);
    }

    [TestMethod]
    public void LoadStringTableFromResourcesFile_MemorySlice_ReturnsStrings()
    {
        byte[] resources = WriteResources(static writer => writer.AddResource("Greeting", "Hello"));
        byte[] padded = new byte[resources.Length + 16];
        resources.CopyTo(padded, 7);
        ReadOnlyMemory<byte> image = new(padded, 7, resources.Length);

        Dictionary<string, string> table = StringResourceTableLoader.LoadStringTableFromResourcesFile(image);

        table["Greeting"].Should().Be("Hello");
    }

    [TestMethod]
    public void LoadStringTableFromResourcesFile_BadMagic_ThrowsArgumentException()
    {
        Action action = () => StringResourceTableLoader.LoadStringTableFromResourcesFile(new byte[64]);

        action.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void LoadStringTableFromResourcesFile_InvalidOptions_ThrowsArgumentOutOfRangeException()
    {
        Action action = () => StringResourceTableLoader.LoadStringTableFromResourcesFile(
            ReadOnlyMemory<byte>.Empty,
            (StringResourceManagerOptions)int.MaxValue);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void LoadStringTableFromResourcesFile_DuplicateName_ThrowsBadImageFormatException()
    {
        byte[] resources = WriteResources(static writer =>
        {
            writer.AddResource("Alpha", "One");
            writer.AddResource("Bravo", "Two");
        });
        ReplaceUtf16(resources, "Bravo", "Alpha");

        Action action = () => StringResourceTableLoader.LoadStringTableFromResourcesFile(resources);

        action.Should().Throw<BadImageFormatException>();
    }

    [TestMethod]
    public void LoadStringTableFromResourcesFile_IgnoreNonStringsDoesNotRetainDuplicateNonStringName()
    {
        byte[] resources = WriteResources(static writer =>
        {
            writer.AddResource("Alpha", "One");
            writer.AddResource("Bravo", 2);
        });
        ReplaceUtf16(resources, "Bravo", "Alpha");

        Dictionary<string, string> table = StringResourceTableLoader.LoadStringTableFromResourcesFile(
            resources,
            StringResourceManagerOptions.IgnoreNonStringResources);

        table.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, string>("Alpha", "One"));
    }

    [TestMethod]
    public void LoadStringTableFromResourcesFile_IgnoreNonStringDuplicate_ReturnsEmpty()
    {
        byte[] resources = WriteResources(static writer =>
        {
            writer.AddResource("Alpha", 1);
            writer.AddResource("Bravo", 2);
        });
        ReplaceUtf16(resources, "Bravo", "Alpha");

        Dictionary<string, string> table = StringResourceTableLoader.LoadStringTableFromResourcesFile(
            resources,
            StringResourceManagerOptions.IgnoreNonStringResources);

        table.Should().BeEmpty();
    }

    [TestMethod]
    public void LoadStringTableFromResourcesFile_UserTypeIndexIsOutOfRange_ThrowsBadImageFormatException()
    {
        using MemoryStream stream = new();
        using (PreserializedResourceWriter writer = new(stream))
        {
            writer.AddResource("Fancy", "10,20", "System.Drawing.Point, System.Drawing.Primitives");
            writer.Generate();
        }

        byte[] resources = stream.ToArray();
        using (RawResourceReader reader = new(resources))
        {
            ResourceLocation location = reader.GetLocation(0);
            location.TypeCode.Should().Be(ResourceTypeCode.StartOfUserTypes);
            resources[location.ContentOffset - 1]++;
        }

        Action action = () => StringResourceTableLoader.LoadStringTableFromResourcesFile(resources);

        action.Should().Throw<BadImageFormatException>();
    }

    private static int GetEmbeddedResourceContentOffset(byte[] image, string resourceName)
    {
        using MemoryStream stream = new(image, writable: false);
        using PEReader peReader = new(stream);
        MetadataReader metadataReader = peReader.GetMetadataReader();

        foreach (ManifestResourceHandle handle in metadataReader.ManifestResources)
        {
            ManifestResource resource = metadataReader.GetManifestResource(handle);
            if (!resource.Implementation.IsNil
                || !metadataReader.StringComparer.Equals(resource.Name, resourceName))
            {
                continue;
            }

            CorHeader corHeader = peReader.PEHeaders.CorHeader
                ?? throw new InvalidOperationException("The test assembly does not have a CLR header.");
            peReader.PEHeaders.TryGetDirectoryOffset(corHeader.ResourcesDirectory, out int directoryOffset)
                .Should().BeTrue();
            return checked(directoryOffset + (int)resource.Offset + sizeof(int));
        }

        throw new InvalidOperationException($"The test assembly does not contain '{resourceName}'.");
    }

    private static void ReplaceUtf16(byte[] data, string oldValue, string newValue)
    {
        byte[] oldBytes = Encoding.Unicode.GetBytes(oldValue);
        byte[] newBytes = Encoding.Unicode.GetBytes(newValue);
        oldBytes.Length.Should().Be(newBytes.Length);

        int match = -1;
        for (int i = 0; i <= data.Length - oldBytes.Length; i++)
        {
            if (!data.AsSpan(i, oldBytes.Length).SequenceEqual(oldBytes))
            {
                continue;
            }

            match.Should().Be(-1, "the resource name should occur exactly once");
            match = i;
        }

        match.Should().BeGreaterThanOrEqualTo(0);
        newBytes.CopyTo(data, match);
    }
}