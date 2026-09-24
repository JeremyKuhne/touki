// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Resources;

namespace Touki.Resources;

[TestClass]
public class ManagedAssemblyMetadataReaderTests
{
    private const string NeutralAttribute = "NeutralResourcesLanguageAttribute";
    private const string ContractAttribute = "SatelliteContractVersionAttribute";

    [TestMethod]
    public void ReadResourceAssembly_NeutralCultureAndContractVersion_ReturnsBindingMetadata()
    {
        using MetadataReaderProvider provider = CreateMetadata(
            (NeutralAttribute, 2, AttributeValue(
                "en-US",
                fallbackLocation: (int)UltimateResourceFallbackLocation.MainAssembly)),
            (ContractAttribute, 1, AttributeValue("2.3.4.5")));

        ResourceAssemblyMetadata metadata = ManagedAssemblyMetadataReader.ReadResourceAssembly(
            provider.GetMetadataReader());

        metadata.ResourceAssemblyIdentity?.Name.Should().Be("Owner");
        metadata.SatelliteAssemblyFileName.Should().Be("Owner.resources.dll");
        metadata.NeutralCultureName.Should().Be("en-US");
        metadata.SatelliteContractVersion.Should().Be(new Version(2, 3, 4, 5));
    }

    [TestMethod]
    public void ReadResourceAssembly_DuplicateNeutralCulture_ThrowsBadImageFormatException()
    {
        using MetadataReaderProvider provider = CreateMetadata(
            (NeutralAttribute, 1, AttributeValue("en-US")),
            (NeutralAttribute, 1, AttributeValue("en-US")));

        Action action = () => ManagedAssemblyMetadataReader.ReadResourceAssembly(provider.GetMetadataReader());

        action.Should().Throw<BadImageFormatException>()
            .WithMessage("The assembly contains multiple neutral resources language attributes.");
    }

    [TestMethod]
    public void ReadResourceAssembly_InvalidNeutralConstructor_ThrowsBadImageFormatException()
    {
        using MetadataReaderProvider provider = CreateMetadata(
            (NeutralAttribute, 0, AttributeValue("en-US")));

        Action action = () => ManagedAssemblyMetadataReader.ReadResourceAssembly(provider.GetMetadataReader());

        action.Should().Throw<BadImageFormatException>()
            .WithMessage("The neutral resources language attribute constructor is invalid.");
    }

    [TestMethod]
    public void ReadResourceAssembly_NullNeutralCulture_ThrowsBadImageFormatException()
    {
        using MetadataReaderProvider provider = CreateMetadata(
            (NeutralAttribute, 1, AttributeValue(serializedString: null)));

        Action action = () => ManagedAssemblyMetadataReader.ReadResourceAssembly(provider.GetMetadataReader());

        action.Should().Throw<BadImageFormatException>()
            .WithMessage("The neutral resources language attribute culture is null.");
    }

    [TestMethod]
    public void ReadResourceAssembly_NeutralResourcesInSatellite_ThrowsNotSupportedException()
    {
        using MetadataReaderProvider provider = CreateMetadata(
            (NeutralAttribute, 2, AttributeValue(
                "en-US",
                fallbackLocation: (int)UltimateResourceFallbackLocation.Satellite)));

        Action action = () => ManagedAssemblyMetadataReader.ReadResourceAssembly(provider.GetMetadataReader());

        action.Should().Throw<NotSupportedException>()
            .WithMessage("Neutral resources stored in a satellite are not supported.");
    }

    [TestMethod]
    public void ReadResourceAssembly_InvalidNeutralFallback_ThrowsBadImageFormatException()
    {
        using MetadataReaderProvider provider = CreateMetadata(
            (NeutralAttribute, 2, AttributeValue("en-US", fallbackLocation: 42)));

        Action action = () => ManagedAssemblyMetadataReader.ReadResourceAssembly(provider.GetMetadataReader());

        action.Should().Throw<BadImageFormatException>()
            .WithMessage("The neutral resources language attribute fallback location is invalid.");
    }

    [TestMethod]
    public void ReadResourceAssembly_DuplicateContractVersion_ThrowsBadImageFormatException()
    {
        using MetadataReaderProvider provider = CreateMetadata(
            (ContractAttribute, 1, AttributeValue("1.2.3.4")),
            (ContractAttribute, 1, AttributeValue("1.2.3.4")));

        Action action = () => ManagedAssemblyMetadataReader.ReadResourceAssembly(provider.GetMetadataReader());

        action.Should().Throw<BadImageFormatException>()
            .WithMessage("The assembly contains multiple satellite contract version attributes.");
    }

    [TestMethod]
    public void ReadResourceAssembly_InvalidContractConstructor_ThrowsBadImageFormatException()
    {
        using MetadataReaderProvider provider = CreateMetadata(
            (ContractAttribute, 2, AttributeValue("1.2.3.4")));

        Action action = () => ManagedAssemblyMetadataReader.ReadResourceAssembly(provider.GetMetadataReader());

        action.Should().Throw<BadImageFormatException>()
            .WithMessage("The satellite contract version attribute constructor is invalid.");
    }

    [TestMethod]
    public void ReadResourceAssembly_InvalidContractVersion_ThrowsBadImageFormatException()
    {
        using MetadataReaderProvider provider = CreateMetadata(
            (ContractAttribute, 1, AttributeValue("invalid")));

        Action action = () => ManagedAssemblyMetadataReader.ReadResourceAssembly(provider.GetMetadataReader());

        action.Should().Throw<BadImageFormatException>()
            .WithMessage("The satellite contract version is invalid.");
    }

    [TestMethod]
    public void ReadResourceAssembly_NullContractVersion_ThrowsBadImageFormatException()
    {
        using MetadataReaderProvider provider = CreateMetadata(
            (ContractAttribute, 1, AttributeValue(serializedString: null)));

        Action action = () => ManagedAssemblyMetadataReader.ReadResourceAssembly(provider.GetMetadataReader());

        action.Should().Throw<BadImageFormatException>()
            .WithMessage("The satellite contract version is invalid.");
    }

    [TestMethod]
    public void ReadResourceAssembly_InvalidAttributeProlog_ThrowsBadImageFormatException()
    {
        using MetadataReaderProvider provider = CreateMetadata(
            (NeutralAttribute, 1, AttributeValue("en-US", prolog: 0)));

        Action action = () => ManagedAssemblyMetadataReader.ReadResourceAssembly(provider.GetMetadataReader());

        action.Should().Throw<BadImageFormatException>()
            .WithMessage("The custom attribute prolog is invalid.");
    }

    [TestMethod]
    public void ReadResourceAssembly_NamedAttributeArgument_ThrowsBadImageFormatException()
    {
        using MetadataReaderProvider provider = CreateMetadata(
            (NeutralAttribute, 1, AttributeValue("en-US", namedArgumentCount: 1)));

        Action action = () => ManagedAssemblyMetadataReader.ReadResourceAssembly(provider.GetMetadataReader());

        action.Should().Throw<BadImageFormatException>()
            .WithMessage("The resource assembly attribute value is invalid.");
    }

    [TestMethod]
    public void ReadResourceAssembly_TrailingAttributeData_ThrowsBadImageFormatException()
    {
        using MetadataReaderProvider provider = CreateMetadata(
            (NeutralAttribute, 1, AttributeValue("en-US", extraData: true)));

        Action action = () => ManagedAssemblyMetadataReader.ReadResourceAssembly(provider.GetMetadataReader());

        action.Should().Throw<BadImageFormatException>()
            .WithMessage("The resource assembly attribute value is invalid.");
    }

    [TestMethod]
    public void ReadResourceAssembly_AssemblyNameIsEmpty_ThrowsBadImageFormatException()
    {
        using MetadataReaderProvider provider = CreateMetadata(
            string.Empty,
            cultureName: null,
            publicKeyOrToken: null,
            flags: default);

        Action action = () => ManagedAssemblyMetadataReader.ReadResourceAssembly(provider.GetMetadataReader());

        action.Should().Throw<BadImageFormatException>()
            .WithMessage("The assembly definition does not have a simple name.");
    }

    [TestMethod]
    public void ReadResourceAssembly_AssemblyHasCultureAndPublicKeyToken_PreservesIdentity()
    {
        using MetadataReaderProvider provider = CreateMetadata(
            "Owner",
            "fr",
            [1, 2, 3],
            flags: default);

        ResourceAssemblyMetadata metadata = ManagedAssemblyMetadataReader.ReadResourceAssembly(
            provider.GetMetadataReader());

        ManagedAssemblyIdentity identity = metadata.ResourceAssemblyIdentity
            ?? throw new InvalidOperationException("The assembly identity was not read.");

        identity.CultureName.Should().Be("fr");
        Action action = () => identity.ValidateMatches(new("Owner", new(1, 2, 3, 4), "fr", [1, 2, 3]));
        action.Should().NotThrow();
    }

    private static MetadataReaderProvider CreateMetadata(
        params (string AttributeName, int ParameterCount, byte[] Value)[] attributes)
    {
        return CreateMetadata(
            assemblyName: "Owner",
            cultureName: null,
            publicKeyOrToken: null,
            flags: default,
            attributes: attributes);
    }

    private static MetadataReaderProvider CreateMetadata(
        string assemblyName,
        string? cultureName,
        byte[]? publicKeyOrToken,
        AssemblyFlags flags,
        params (string AttributeName, int ParameterCount, byte[] Value)[] attributes)
    {
        MetadataBuilder builder = new();
        builder.AddModule(
            0,
            builder.GetOrAddString("Owner.dll"),
            builder.GetOrAddGuid(Guid.NewGuid()),
            encId: default,
            encBaseId: default);

        AssemblyDefinitionHandle assembly = builder.AddAssembly(
            builder.GetOrAddString(assemblyName),
            new(1, 2, 3, 4),
            cultureName is null ? default : builder.GetOrAddString(cultureName),
            publicKeyOrToken is null ? default : builder.GetOrAddBlob(publicKeyOrToken),
            flags,
            AssemblyHashAlgorithm.None);

        AssemblyReferenceHandle runtime = builder.AddAssemblyReference(
            builder.GetOrAddString("System.Runtime"),
            new(10, 0, 0, 0),
            culture: default,
            publicKeyOrToken: default,
            flags: default,
            hashValue: default);

        TypeReferenceHandle fallbackType = builder.AddTypeReference(
            runtime,
            builder.GetOrAddString("System.Resources"),
            builder.GetOrAddString(nameof(UltimateResourceFallbackLocation)));

        foreach ((string attributeName, int parameterCount, byte[] value) in attributes)
        {
            TypeReferenceHandle attributeType = builder.AddTypeReference(
                runtime,
                builder.GetOrAddString("System.Resources"),
                builder.GetOrAddString(attributeName));

            BlobBuilder signature = new();
            new BlobEncoder(signature).MethodSignature(isInstanceMethod: true).Parameters(
                parameterCount,
                returnType => returnType.Void(),
                parameters =>
                {
                    for (int i = 0; i < parameterCount; i++)
                    {
                        if (attributeName == NeutralAttribute && i == 1)
                        {
                            parameters.AddParameter().Type().Type(fallbackType, isValueType: true);
                        }
                        else
                        {
                            parameters.AddParameter().Type().String();
                        }
                    }
                });

            MemberReferenceHandle constructor = builder.AddMemberReference(
                attributeType,
                builder.GetOrAddString(".ctor"),
                builder.GetOrAddBlob(signature));

            builder.AddCustomAttribute(assembly, constructor, builder.GetOrAddBlob(value));
        }

        BlobBuilder image = new();
        new MetadataRootBuilder(builder).Serialize(image, methodBodyStreamRva: 0, mappedFieldDataStreamRva: 0);
        return MetadataReaderProvider.FromMetadataImage([.. image.ToArray()]);
    }

    private static byte[] AttributeValue(
        string? serializedString,
        int? fallbackLocation = null,
        ushort prolog = 1,
        ushort namedArgumentCount = 0,
        bool extraData = false)
    {
        BlobBuilder value = new();
        value.WriteUInt16(prolog);
        if (serializedString is null)
        {
            // 0xFF encodes a null serialized string in a custom attribute blob.
            value.WriteByte(0xff);
        }
        else
        {
            value.WriteSerializedString(serializedString);
        }

        if (fallbackLocation is int location)
        {
            value.WriteInt32(location);
        }

        value.WriteUInt16(namedArgumentCount);
        if (extraData)
        {
            value.WriteByte(0);
        }

        return value.ToArray();
    }
}
