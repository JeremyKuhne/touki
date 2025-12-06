// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Originally from WinForms
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace TestSupport;

public class TypeExtensionTests
{
    [Fact]
    public void GetFullNestedType_NonGenericNested_ReturnsNestedType()
    {
        Type parentType = typeof(OuterClass);
        Type nestedType = parentType.GetFullNestedType("NonGenericNested");
        nestedType.Should().Be<OuterClass.NonGenericNested>();
    }

    [Fact]
    public void GetFullNestedType_GenericNestedWithParentGenerics_ReturnsConstructedNestedType()
    {
        Type parentType = typeof(OuterClass<int, string>);
        Type nestedType = parentType.GetFullNestedType("GenericNested", typeof(double));
        nestedType.Should().Be<OuterClass<int, string>.GenericNested<double>>();
    }

    [Fact]
    public void GetFullNestedType_GenericNestedWithNonGenericParent_ReturnsConstructedNestedType()
    {
        Type parentType = typeof(OuterClass);
        Type nestedType = parentType.GetFullNestedType("GenericNested", typeof(double));
        nestedType.Should().Be<OuterClass.GenericNested<double>>();
    }

    [Fact]
    public void GetFullNestedType_GenericNestedWithMultipleParameters_ReturnsConstructedNestedType()
    {
        Type parentType = typeof(OuterClass);
        Type nestedType = parentType.GetFullNestedType("MultiGenericNested", typeof(int), typeof(string));
        nestedType.Should().Be<OuterClass.MultiGenericNested<int, string>>();
    }

    [Fact]
    public void GetFullNestedType_NonGenericNestedInGenericParent_ReturnsNestedType()
    {
        Type parentType = typeof(OuterClass<int, string>);
        Type nestedType = parentType.GetFullNestedType("NonGenericNested");
        nestedType.Should().Be<OuterClass<int, string>.NonGenericNested>();
    }

    [Fact]
    public void GetFullNestedType_GenericNestedInheritingParentGenerics_ThrowsArgumentException()
    {
        Type parentType = typeof(OuterClass<int, string>);
        Action act = () => parentType.GetFullNestedType("NestedInheritsParentGenerics");
        act.Should().Throw<ArgumentException>()
            .WithMessage("Could not find NestedInheritsParentGenerics in OuterClass`2");
    }

    [Fact]
    public void GetFullNestedType_NonExistentNestedType_ThrowsArgumentException()
    {
        Type parentType = typeof(OuterClass);
        Action act = () => parentType.GetFullNestedType("NonExistentType");
        act.Should().Throw<ArgumentException>()
            .WithMessage("Could not find NonExistentType in OuterClass");
    }

    [Fact]
    public void GetFullNestedType_GenericNestedWithoutParameters_ThrowsArgumentException()
    {
        Type parentType = typeof(OuterClass);
        Action act = () => parentType.GetFullNestedType("GenericNested");
        act.Should().Throw<ArgumentException>()
            .WithMessage("Could not find GenericNested in OuterClass");
    }

    [Fact]
    public void GetFullNestedType_GenericNestedWithoutExplicitParameters_ThrowsArgumentException()
    {
        Type parentType = typeof(OuterClassWithInheritedGenerics<int, string>);
        Action act = () => parentType.GetFullNestedType("GenericNested");
        act.Should().Throw<ArgumentException>()
            .WithMessage("Could not find GenericNested in OuterClassWithInheritedGenerics`2");
    }

    [Fact]
    public void GetFullNestedType_GenericParentDefinition_ThrowsArgumentException()
    {
        Type parentType = typeof(OuterClass<,>);
        Action act = () => parentType.GetFullNestedType("GenericNested", typeof(double));
        act.Should().Throw<ArgumentException>()
            .WithMessage("The parent type cannot be a type definition.*")
            .WithParameterName("type");
    }

    [Fact]
    public void GetFullNestedType_WrongGenericParameterCount_ThrowsArgumentException()
    {
        Type parentType = typeof(OuterClass<int, string>);
        Action act = () => parentType.GetFullNestedType("GenericNested", typeof(double), typeof(float));
        act.Should().Throw<ArgumentException>()
            .WithMessage("Could not find GenericNested in OuterClass`2");
    }

    [Fact]
    public void GetFullNestedType_TooFewGenericParameters_ThrowsArgumentException()
    {
        Type parentType = typeof(OuterClass<int, string>);
        Action act = () => parentType.GetFullNestedType("GenericNested");
        act.Should().Throw<ArgumentException>()
            .WithMessage("Could not find GenericNested in OuterClass`2");
    }

    [Fact]
    public void GetFullNestedType_TooFewGenericParametersForNonGenericParent_ThrowsArgumentException()
    {
        Type parentType = typeof(OuterClass);
        Action act = () => parentType.GetFullNestedType("MultiGenericNested", typeof(int));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetFullNestedType_TooManyGenericParametersForNonGenericParent_ThrowsArgumentException()
    {
        Type parentType = typeof(OuterClass);
        Action act = () => parentType.GetFullNestedType("GenericNested", typeof(int), typeof(string), typeof(double));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetFullNestedType_EmptyNestedTypeName_ThrowsArgumentException()
    {
        Type parentType = typeof(OuterClass);
        Action act = () => parentType.GetFullNestedType("");
        act.Should().Throw<ArgumentException>()
            .WithMessage("Could not find  in OuterClass");
    }

    [Fact]
    public void GetFullNestedType_PublicNestedType_ReturnsNestedType()
    {
        Type parentType = typeof(OuterClass);
        Type nestedType = parentType.GetFullNestedType("PublicNested");
        nestedType.Should().Be<OuterClass.PublicNested>();
    }

    [Fact]
    public void GetFullNestedType_PrivateNestedType_ReturnsNestedType()
    {
        Type parentType = typeof(OuterClass);
        Type nestedType = parentType.GetFullNestedType("PrivateNested");
        nestedType.Should().NotBeNull();
        nestedType.Name.Should().Be("PrivateNested");
    }

    [Fact]
    public void GetFullNestedType_ComplexGenericCombination_ReturnsConstructedNestedType()
    {
        Type parentType = typeof(OuterClass<List<int>, Dictionary<string, double>>);
        Type nestedType = parentType.GetFullNestedType("GenericNested", typeof(HashSet<bool>));
        nestedType.Should().NotBeNull();
        nestedType.GenericTypeArguments.Should().HaveCount(3);
        nestedType.GenericTypeArguments[0].Should().Be(typeof(List<int>));
        nestedType.GenericTypeArguments[1].Should().Be(typeof(Dictionary<string, double>));
        nestedType.GenericTypeArguments[2].Should().Be(typeof(HashSet<bool>));
    }

    [Fact]
    public void GetFullNestedType_NestedGenericDefinitionRequiresParameter_ThrowsArgumentException()
    {
        Type parentType = typeof(OuterClass);
        Type nestedType = parentType.GetFullNestedType("GenericNested", typeof(int));
        Action act = () => nestedType.GetFullNestedType("DeeplyNested");
        act.Should().Throw<ArgumentException>()
            .WithMessage("Could not find DeeplyNested in GenericNested`1");
    }

    [Fact]
    public void GetFullNestedType_ParameterCountMismatchWithParentGenerics_ThrowsArgumentException()
    {
        Type parentType = typeof(OuterClassWithInheritedGenerics<int, string>);
        Action act = () => parentType.GetFullNestedType("GenericNested", typeof(double));
        act.Should().Throw<ArgumentException>()
            .WithMessage("Could not find GenericNested in OuterClassWithInheritedGenerics`2");
    }

#pragma warning disable CA1052 // Static holder types should be Static or NotInheritable
    internal class OuterClass
    {
        internal class NonGenericNested { }
        internal class GenericNested<T>
        {
            internal class DeeplyNested<U> { }
        }
        internal class MultiGenericNested<T1, T2> { }
        public class PublicNested { }
        private class PrivateNested { }
    }

    internal class OuterClass<T1, T2>
    {
        internal class NonGenericNested { }
        internal class GenericNested<T3> { }
        internal class NestedInheritsParentGenerics<U1, U2> { }
    }

    internal class OuterClassWithInheritedGenerics<T1, T2>
    {
        internal class GenericNested<U1, U2> { }
    }
#pragma warning restore CA1052
}
