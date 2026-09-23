// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Buffers.Binary;
using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Touki.Resources;

public partial class SatelliteStringResourceManagerTests
{
    private const int CorHeaderResourcesDirectorySizeOffset = 28;

    [TestMethod]
    public void GetString_SatelliteResourceDirectoryAtRawSectionBoundary_ReturnsLocalizedValue()
    {
        using TempFolder folder = new();
        byte[] image = ReadSatelliteAssembly();
        (int SizeOffset, int MaximumSize) bounds = GetResourceDirectorySizeBounds(image);
        BinaryPrimitives.WriteInt32LittleEndian(
            image.AsSpan(bounds.SizeOffset, sizeof(int)),
            bounds.MaximumSize);

        WriteSatelliteAssembly(folder.TempPath, image);

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            NeutralBaseName(),
            folder.TempPath,
            s_assembly);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_SatelliteResourceDirectoryPastRawSectionBoundary_ThrowsBadImageFormatException()
    {
        using TempFolder folder = new();
        byte[] image = ReadSatelliteAssembly();
        (int SizeOffset, int MaximumSize) bounds = GetResourceDirectorySizeBounds(image);
        BinaryPrimitives.WriteInt32LittleEndian(
            image.AsSpan(bounds.SizeOffset, sizeof(int)),
            checked(bounds.MaximumSize + 1));

        WriteSatelliteAssembly(folder.TempPath, image);

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            NeutralBaseName(),
            folder.TempPath,
            s_assembly);

        Action action = () => manager.GetString("Greeting", new CultureInfo("de"));

        action.Should().Throw<BadImageFormatException>();
    }

    [TestMethod]
    public void GetString_SatelliteAssemblyWithOversizedResourceLength_ThrowsBadImageFormatException()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        byte[] image = ReadSatelliteAssembly();
        int lengthOffset = GetEmbeddedResourceLengthOffset(image, $"{baseName}.de.resources");
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(lengthOffset, sizeof(int)), int.MaxValue);
        WriteSatelliteAssembly(folder.TempPath, image);

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        Action action = () => manager.GetString("Greeting", new CultureInfo("de"));

        action.Should().Throw<BadImageFormatException>();
    }

    [TestMethod]
    public void LoadIndexedTableFromAssembly_InvalidResource_DisposesOwnerOnce()
    {
        string resourceName = $"{NeutralBaseName()}.de.resources";
        byte[] image = ReadSatelliteAssembly();
        int lengthOffset = GetEmbeddedResourceLengthOffset(image, resourceName);
        image.AsSpan(lengthOffset + sizeof(int), sizeof(int)).Clear();
        ThrowOnSecondDispose owner = new();

        Action action = () => StringResourceTableLoader.LoadIndexedTableFromAssembly(
            image,
            resourceName,
            StringResourceManagerOptions.None,
            owner);

        action.Should().Throw<ArgumentException>();
        owner.DisposeCount.Should().Be(1);
    }

    private static byte[] ReadSatelliteAssembly() => System.IO.File.ReadAllBytes(
        Path.Join(AppContext.BaseDirectory, "de", SatelliteAssemblyFileName()));

    private static void WriteSatelliteAssembly(string probeRoot, byte[] image)
    {
        string directory = Path.Join(probeRoot, "de");
        Directory.CreateDirectory(directory);
        System.IO.File.WriteAllBytes(Path.Join(directory, SatelliteAssemblyFileName()), image);
    }

    private static void ReplaceManifestResourceName(
        byte[] image,
        string resourceName,
        string replacementName)
    {
        byte[] resourceNameBytes = Encoding.UTF8.GetBytes(resourceName);
        byte[] replacementNameBytes = Encoding.UTF8.GetBytes(replacementName);
        replacementNameBytes.Should().HaveSameCount(resourceNameBytes);
        int offset = image.AsSpan().IndexOf(resourceNameBytes);
        offset.Should().BeGreaterThanOrEqualTo(0);
        replacementNameBytes.CopyTo(image, offset);
    }

    private static (int SizeOffset, int MaximumSize) GetResourceDirectorySizeBounds(byte[] image)
    {
        using MemoryStream stream = new(image, writable: false);
        using PEReader peReader = new(stream);
        PEHeaders headers = peReader.PEHeaders;
        CorHeader corHeader = headers.CorHeader
            ?? throw new InvalidOperationException("The test satellite does not have a CLR header.");

        PEHeader peHeader = headers.PEHeader
            ?? throw new InvalidOperationException("The test satellite does not have a PE header.");

        headers.TryGetDirectoryOffset(peHeader.CorHeaderTableDirectory, out int corHeaderOffset)
            .Should().BeTrue();

        headers.TryGetDirectoryOffset(corHeader.ResourcesDirectory, out int resourceDirectoryOffset)
            .Should().BeTrue();

        foreach (SectionHeader section in headers.SectionHeaders)
        {
            long sectionRelativeOffset = (long)corHeader.ResourcesDirectory.RelativeVirtualAddress
                - section.VirtualAddress;

            if (sectionRelativeOffset < 0
                || (long)section.PointerToRawData + sectionRelativeOffset != resourceDirectoryOffset)
            {
                continue;
            }

            int maximumSize = checked(section.SizeOfRawData - (int)sectionRelativeOffset);
            return (corHeaderOffset + CorHeaderResourcesDirectorySizeOffset, maximumSize);
        }

        throw new InvalidOperationException("The test satellite resource directory is not in a PE section.");
    }

    private static int GetEmbeddedResourceLengthOffset(byte[] image, string resourceName)
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
                ?? throw new InvalidOperationException("The test satellite does not have a CLR header.");

            peReader.PEHeaders.TryGetDirectoryOffset(corHeader.ResourcesDirectory, out int directoryOffset)
                .Should().BeTrue();

            return checked(directoryOffset + (int)resource.Offset);
        }

        throw new InvalidOperationException($"The test satellite does not contain '{resourceName}'.");
    }

    private sealed class ThrowOnSecondDispose : IDisposable
    {
        internal int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
            if (DisposeCount > 1)
            {
                throw new InvalidOperationException("The owner was disposed more than once.");
            }
        }
    }
}