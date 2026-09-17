// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class StoringNull
{
    [TestMethod]
    public void TryGetValue_EmptyAsNullableValue_ReturnsTrueWithNoValue()
    {
        Value value = Value.Create((int?)null);

        value.TryGetValue(out int? result).Should().BeTrue();
        result.Should().BeNull();
    }

    [TestMethod]
    public void TryGetValue_PresentNullableReference_NarrowsResult()
    {
        Value value = Value.Create("text");

        if (value.TryGetValue<string>(out string? result))
        {
            result.Length.Should().Be(4);
        }
        else
        {
            Assert.Fail("The stored string should be retrievable.");
        }
    }

    [TestMethod]
    public void As_NonNullableReference_HasNonNullableReturnContract()
    {
        Value value = Value.Create("text");

        string result = value.As<string>();

        result.Should().Be("text");
    }

    [TestMethod]
    public void GetIntFromStoredNull()
    {
        Value nullFastValue = Value.Create((object?)null);
        Assert.Throws<InvalidCastException>(() => _ = nullFastValue.As<int>());

        bool success = nullFastValue.TryGetValue(out int result);
        success.Should().BeFalse();

        result.Should().Be(default);
    }
}
