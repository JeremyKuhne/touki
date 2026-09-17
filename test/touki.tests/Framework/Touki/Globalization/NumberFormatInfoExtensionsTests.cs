// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;

namespace Touki.Globalization;

[TestClass]
public class NumberFormatInfoExtensionsTests
{
    [TestMethod]
    public void GetNumberGroupSizes_DifferentInstances_ReturnsInstanceBackingArray()
    {
        NumberFormatInfo first = new() { NumberGroupSizes = [3] };
        NumberFormatInfo second = new() { NumberGroupSizes = [3, 2] };

        int[] firstGroups = first.GetNumberGroupSizes();
        int[] secondGroups = second.GetNumberGroupSizes();

        firstGroups.Should().Equal(3);
        secondGroups.Should().Equal(3, 2);
        firstGroups.Should().NotBeSameAs(secondGroups);
    }

    [TestMethod]
    public void GetCurrencyGroupSizes_DifferentInstances_ReturnsInstanceBackingArray()
    {
        NumberFormatInfo first = new() { CurrencyGroupSizes = [3] };
        NumberFormatInfo second = new() { CurrencyGroupSizes = [2, 0] };

        int[] firstGroups = first.GetCurrencyGroupSizes();
        int[] secondGroups = second.GetCurrencyGroupSizes();

        firstGroups.Should().Equal(3);
        secondGroups.Should().Equal(2, 0);
        firstGroups.Should().NotBeSameAs(secondGroups);
    }

    [TestMethod]
    public void GetPercentGroupSizes_DifferentInstances_ReturnsInstanceBackingArray()
    {
        NumberFormatInfo first = new() { PercentGroupSizes = [3] };
        NumberFormatInfo second = new() { PercentGroupSizes = [4, 3] };

        int[] firstGroups = first.GetPercentGroupSizes();
        int[] secondGroups = second.GetPercentGroupSizes();

        firstGroups.Should().Equal(3);
        secondGroups.Should().Equal(4, 3);
        firstGroups.Should().NotBeSameAs(secondGroups);
    }
}