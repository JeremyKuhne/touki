// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class TypeInfoTests
{
    [Fact]
    public void IsReferenceOrContainsReferences_ReferenceTypes_ReturnsTrue()
    {
        // Reference types should return true
        TypeInfo<string>.IsReferenceOrContainsReferences().Should().BeTrue();
        TypeInfo<object>.IsReferenceOrContainsReferences().Should().BeTrue();
        TypeInfo<TypeInfoTests>.IsReferenceOrContainsReferences().Should().BeTrue();
    }

    [Fact]
    public void IsReferenceOrContainsReferences_ValueTypesWithoutReferences_ReturnsFalse()
    {
        // Value types without references should return false
        TypeInfo<int>.IsReferenceOrContainsReferences().Should().BeFalse();
        TypeInfo<bool>.IsReferenceOrContainsReferences().Should().BeFalse();
        TypeInfo<DateTime>.IsReferenceOrContainsReferences().Should().BeFalse();
        TypeInfo<TestEnum>.IsReferenceOrContainsReferences().Should().BeFalse();
        TypeInfo<SimpleStruct>.IsReferenceOrContainsReferences().Should().BeFalse();
    }

    [Fact]
    public void IsReferenceOrContainsReferences_ValueTypesWithReferences_ReturnsTrue()
    {
        // Value types containing references should return true
        TypeInfo<StructWithReference>.IsReferenceOrContainsReferences().Should().BeTrue();
        TypeInfo<StructWithString>.IsReferenceOrContainsReferences().Should().BeTrue();
    }

    [Fact]
    public void IsReferenceOrContainsReferences_ResultIsCached()
    {
        // First call should compute the result
        bool firstResult = TypeInfo<int>.IsReferenceOrContainsReferences();

        // Second call should use cached result
        bool secondResult = TypeInfo<int>.IsReferenceOrContainsReferences();

        // Both calls should return the same result
        secondResult.Should().Be(firstResult);
    }

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    private enum TestEnum
    {
        Value1,
        Value2
    }

    private struct SimpleStruct
    {
        public int X;
        public int Y;
    }

    private struct StructWithReference
    {
        public object Reference;
        public int Value;
    }

    private struct StructWithString
    {
        public string Text;
        public double Number;
    }
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
}
