// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class TypeExtensionsTests
{
    [Fact]
    public void IsAssignableTo_SameType_ReturnsTrue()
    {
        typeof(string).IsAssignableTo(typeof(string)).Should().BeTrue();
    }

    [Fact]
    public void IsAssignableTo_DerivedToBase_ReturnsTrue()
    {
        typeof(string).IsAssignableTo(typeof(object)).Should().BeTrue();
    }

    [Fact]
    public void IsAssignableTo_BaseToDerived_ReturnsFalse()
    {
        typeof(object).IsAssignableTo(typeof(string)).Should().BeFalse();
    }

    [Fact]
    public void IsAssignableTo_ImplementsInterface_ReturnsTrue()
    {
        typeof(string).IsAssignableTo(typeof(IComparable)).Should().BeTrue();
    }

    [Fact]
    public void IsAssignableTo_DoesNotImplementInterface_ReturnsFalse()
    {
        typeof(int).IsAssignableTo(typeof(IDisposable)).Should().BeFalse();
    }

    [Fact]
    public void IsAssignableTo_UnrelatedTypes_ReturnsFalse()
    {
        typeof(int).IsAssignableTo(typeof(string)).Should().BeFalse();
    }

    [Fact]
    public void IsAssignableTo_ValueTypeToObject_ReturnsTrue()
    {
        typeof(int).IsAssignableTo(typeof(object)).Should().BeTrue();
    }

    [Fact]
    public void IsAssignableTo_NullTargetType_ReturnsFalse()
    {
        typeof(string).IsAssignableTo(null).Should().BeFalse();
    }

    [Fact]
    public void IsAssignableTo_ValueTypeToValueType_ReturnsFalse()
    {
        typeof(int).IsAssignableTo(typeof(long)).Should().BeFalse();
    }

    [Fact]
    public void IsAssignableTo_ArrayToIEnumerable_ReturnsTrue()
    {
        typeof(int[]).IsAssignableTo(typeof(IEnumerable<int>)).Should().BeTrue();
    }

    [Fact]
    public void IsTypeDefinition_RegularClass_ReturnsTrue()
    {
        typeof(string).IsTypeDefinition.Should().BeTrue();
    }

    [Fact]
    public void IsTypeDefinition_ValueType_ReturnsTrue()
    {
        typeof(int).IsTypeDefinition.Should().BeTrue();
    }

    [Fact]
    public void IsTypeDefinition_Interface_ReturnsTrue()
    {
        typeof(IDisposable).IsTypeDefinition.Should().BeTrue();
    }

    [Fact]
    public void IsTypeDefinition_Array_ReturnsFalse()
    {
        typeof(int[]).IsTypeDefinition.Should().BeFalse();
    }

    [Fact]
    public void IsTypeDefinition_ByRef_ReturnsFalse()
    {
        typeof(int).MakeByRefType().IsTypeDefinition.Should().BeFalse();
    }

    [Fact]
    public void IsTypeDefinition_Pointer_ReturnsFalse()
    {
        typeof(int).MakePointerType().IsTypeDefinition.Should().BeFalse();
    }

    [Fact]
    public void IsTypeDefinition_ConstructedGeneric_ReturnsFalse()
    {
        typeof(List<int>).IsTypeDefinition.Should().BeFalse();
    }

    [Fact]
    public void IsTypeDefinition_GenericTypeDefinition_ReturnsTrue()
    {
        typeof(List<>).IsTypeDefinition.Should().BeTrue();
    }

    [Fact]
    public void IsTypeDefinition_GenericParameter_ReturnsFalse()
    {
        Type genericParam = typeof(List<>).GetGenericArguments()[0];
        genericParam.IsTypeDefinition.Should().BeFalse();
    }

    [Fact]
    public void IsTypeDefinition_MultidimensionalArray_ReturnsFalse()
    {
        typeof(int[,]).IsTypeDefinition.Should().BeFalse();
    }

    [Fact]
    public void IsTypeDefinition_Enum_ReturnsTrue()
    {
        typeof(DayOfWeek).IsTypeDefinition.Should().BeTrue();
    }
}
