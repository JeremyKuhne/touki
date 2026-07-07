// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  Tests for <see cref="Value"/>'s C# language-union behaviors (it is a <c>[Union]</c> type via its
///  <c>IUnionMembers</c> provider). Round-trip create/retrieve is covered by the <c>Storing*</c> suites;
///  these cover union pattern matching. Zero-allocation checks live in <c>ValueUnionTests_Memory</c>.
/// </summary>
[TestClass]
public class ValueUnionTests
{
    [TestMethod]
    public void Match_Int_UnwrapsContents()
    {
        Value value = Value.Create(42);
        (value is int).Should().BeTrue();

        if (value is int matched)
        {
            matched.Should().Be(42);
        }
        else
        {
            Assert.Fail("Value should have matched int.");
        }
    }

    [TestMethod]
    public void Match_WrongType_ReturnsFalse()
    {
        Value value = Value.Create(42);
        (value is string).Should().BeFalse();
        (value is long).Should().BeFalse();
        (value is bool).Should().BeFalse();
    }

    [TestMethod]
    public void Match_String_UnwrapsContents()
    {
        Value value = Value.Create("hello");

        if (value is string matched)
        {
            matched.Should().Be("hello");
        }
        else
        {
            Assert.Fail("Value should have matched string.");
        }
    }

    [TestMethod]
    public void Match_Long_UnwrapsContents()
    {
        Value value = Value.Create(long.MaxValue);
        (value is long matched && matched == long.MaxValue).Should().BeTrue();
    }

    [TestMethod]
    public void Match_Double_UnwrapsContents()
    {
        Value value = Value.Create(3.5);
        (value is double matched && matched == 3.5).Should().BeTrue();
    }

    [TestMethod]
    public void Match_Bool_UnwrapsContents()
    {
        Value value = Value.Create(true);
        (value is bool matched && matched).Should().BeTrue();
    }

    [TestMethod]
    public void Match_DateTimeOffset_UnwrapsContents()
    {
        DateTimeOffset source = new(2026, 7, 6, 12, 30, 0, TimeSpan.FromHours(-5));
        Value value = Value.Create(source);
        (value is DateTimeOffset matched && matched == source).Should().BeTrue();
    }

    [TestMethod]
    public void Switch_OverCaseTypes_DispatchesToContents()
    {
        Describe(Value.Create(42)).Should().Be("int:42");
        Describe(Value.Create("hi")).Should().Be("string:hi");
        Describe(Value.Create(true)).Should().Be("bool:True");

        static string Describe(Value value) => value switch
        {
            int i => $"int:{i}",
            bool b => $"bool:{b}",
            string s => $"string:{s}",
            _ => "other",
        };
    }

    [TestMethod]
    public void Match_Default_MatchesNothing()
    {
        Value value = default;
        (value is int).Should().BeFalse();
        (value is string).Should().BeFalse();
    }
}
