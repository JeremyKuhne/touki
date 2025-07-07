// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki;

namespace Framework.Touki;

public class EnumTests
{
    [Fact]
    public void TestGetValuesAndNames()
    {
        var expectedValues = new ulong[] { 0, 1, 2, 3, 4, 5, 6 };
        var expectedNames = new string[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

        var (values, names) = EnumExtensions.GetValuesAndNames<DayOfWeek>();

        values.Should().BeEquivalentTo(expectedValues);
        names.Should().BeEquivalentTo(expectedNames);
    }
}
