// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections;
using System.Formats.Nrbf;
using System.Reflection.Metadata;
using System.Runtime.Serialization;
using Touki.Resources;

namespace Touki;

[TestClass]
public class RegisteredTypeResolverTests
{
    [TestMethod]
    public void Register_CustomType_ReturnsResolverAndResolvesType()
    {
        RegisteredTypeResolver resolver = new();

        RegisteredTypeResolver result = resolver.Register<RegisteredPayload>();

        result.Should().BeSameAs(resolver);
        resolver.BindToType(TypeName.Parse(typeof(RegisteredPayload).AssemblyQualifiedName!))
            .Should().BeSameAs(typeof(RegisteredPayload));
    }

    [TestMethod]
    public void BindToType_RegisteredType_ReturnsType()
    {
        ITypeResolver resolver = new RegisteredTypeResolver().Register<RegisteredPayload>();
        TypeName typeName = TypeName.Parse(typeof(RegisteredPayload).AssemblyQualifiedName!);

        resolver.BindToType(typeName).Should().BeSameAs(typeof(RegisteredPayload));
    }

    [TestMethod]
    public void BindToType_DefaultFrameworkTypes_ReturnsTypes()
    {
        Type[] frameworkTypes =
        [
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(char),
            typeof(bool),
            typeof(string),
            typeof(decimal),
            typeof(DateTime),
            typeof(TimeSpan),
            typeof(nint),
            typeof(nuint),
            typeof(NotSupportedException),
            typeof(List<bool>),
            typeof(List<char>),
            typeof(List<string>),
            typeof(List<sbyte>),
            typeof(List<byte>),
            typeof(List<short>),
            typeof(List<ushort>),
            typeof(List<int>),
            typeof(List<uint>),
            typeof(List<long>),
            typeof(List<ulong>),
            typeof(List<float>),
            typeof(List<double>),
            typeof(List<decimal>),
            typeof(List<DateTime>),
            typeof(List<TimeSpan>),
            typeof(byte[]),
            typeof(sbyte[]),
            typeof(short[]),
            typeof(ushort[]),
            typeof(int[]),
            typeof(uint[]),
            typeof(long[]),
            typeof(ulong[]),
            typeof(float[]),
            typeof(double[]),
            typeof(char[]),
            typeof(bool[]),
            typeof(string[]),
            typeof(decimal[]),
            typeof(DateTime[]),
            typeof(TimeSpan[]),
            typeof(object[]),
            typeof(ArrayList),
            typeof(Hashtable)
        ];

        RegisteredTypeResolver resolver = new();

        foreach (Type frameworkType in frameworkTypes)
        {
            TypeName typeName = TypeName.Parse(frameworkType.AssemblyQualifiedName!);
            resolver.BindToType(typeName).Should().BeSameAs(frameworkType);
        }
    }

    [TestMethod]
    public void BindToType_FrameworkGenericIdentity_ReturnsModernType()
    {
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.ListInt32);
        ClassRecord rootRecord = (ClassRecord)formatted.RootRecord;
        RegisteredTypeResolver resolver = new();

        resolver.BindToType(rootRecord.TypeName).Should().BeSameAs(typeof(List<int>));
    }

    [TestMethod]
    public void BindToType_NullTypeName_ThrowsArgumentNullException()
    {
        RegisteredTypeResolver resolver = new();

        Action action = () => resolver.BindToType(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void BindToType_VariableBoundArrayWithRegisteredSzArray_ThrowsSerializationException()
    {
        RegisteredTypeResolver resolver = new();
        TypeName variableBoundArray = TypeName.Parse(typeof(int).MakeArrayType(1).AssemblyQualifiedName!);

        Action action = () => resolver.BindToType(variableBoundArray);

        action.Should().Throw<SerializationException>().WithMessage("*is not registered*");
    }

    [TestMethod]
    public void BindToType_DrawingType_ThrowsSerializationException()
    {
        RegisteredTypeResolver resolver = new();
        TypeName bitmap = TypeName.Parse("System.Drawing.Bitmap, System.Drawing");

        Action action = () => resolver.BindToType(bitmap);

        action.Should().Throw<SerializationException>().WithMessage("*is not registered*");
    }

    [TestMethod]
    public void TryBindToType_RegisteredType_ReturnsTrueAndType()
    {
        ITypeResolver resolver = new RegisteredTypeResolver().Register<RegisteredPayload>();
        TypeName typeName = TypeName.Parse(typeof(RegisteredPayload).AssemblyQualifiedName!);

        bool result = resolver.TryBindToType(typeName, out Type? type);

        result.Should().BeTrue();
        type.Should().BeSameAs(typeof(RegisteredPayload));
    }

    [TestMethod]
    public void TryBindToType_UnregisteredType_ReturnsFalseAndNull()
    {
        ITypeResolver resolver = new RegisteredTypeResolver();
        TypeName typeName = TypeName.Parse("System.Drawing.Bitmap, System.Drawing");

        bool result = resolver.TryBindToType(typeName, out Type? type);

        result.Should().BeFalse();
        type.Should().BeNull();
    }
}