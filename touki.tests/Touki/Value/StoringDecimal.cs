// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class StoringDecimal
{
    public static IEnumerable<object[]> DecimalData()
    {
        yield return [(decimal)42];
        yield return [decimal.MaxValue];
        yield return [decimal.MinValue];
    }

    [TestMethod]
    public void DecimalImplicit()
    {
        Value value = (decimal)42.0;
        value.As<decimal>().Should().Be((decimal)42.0);
        value.Type.Should().Be(typeof(decimal));

        decimal? source = (decimal?)42.0;
        value = source;
        value.As<decimal?>().Should().Be(source);
        value.Type.Should().Be(typeof(decimal));
    }

    [TestMethod]
    [DynamicData(nameof(DecimalData))]
    public void DecimalInOut(decimal @decimal)
    {
        Value value = @decimal;
        bool success = value.TryGetValue(out decimal result);
        success.Should().BeTrue();
        result.Should().Be(@decimal);

        value.As<decimal>().Should().Be(@decimal);
        ((decimal)value).Should().Be(@decimal);
    }

    [TestMethod]
    [DynamicData(nameof(DecimalData))]
    public void NullableDecimalInDecimalOut(decimal @decimal)
    {
        decimal? source = @decimal;
        Value value = Value.Create(source);

        bool success = value.TryGetValue(out decimal result);
        success.Should().BeTrue();
        result.Should().Be(@decimal);

        value.As<decimal>().Should().Be(@decimal);

        ((decimal)value).Should().Be(@decimal);
    }

    [TestMethod]
    [DynamicData(nameof(DecimalData))]
    public void DecimalInNullableDecimalOut(decimal @decimal)
    {
        decimal source = @decimal;
        Value value = Value.Create(source);
        bool success = value.TryGetValue(out decimal? result);
        success.Should().BeTrue();
        result.Should().Be(@decimal);

        ((decimal?)value).Should().Be(@decimal);
    }

    [TestMethod]
    public void NullDecimal()
    {
        decimal? source = null;
        Value value = source;
        value.Type.Should().BeNull();
        value.As<decimal?>().Should().Be(source);
        value.As<decimal?>().HasValue.Should().BeFalse();
    }

    [TestMethod]
    [DynamicData(nameof(DecimalData))]
    public void OutAsObject(decimal @decimal)
    {
        Value value = Value.Create(@decimal);
        object o = value.As<object>();
        o.GetType().Should().Be(typeof(decimal));
        ((decimal)o).Should().Be(@decimal);

        decimal? n = @decimal;
        value = n;
        o = value.As<object>();
        o.GetType().Should().Be(typeof(decimal));
        ((decimal)o).Should().Be(@decimal);
    }
}
