// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class StoringShort
{
    public static IEnumerable<object[]> ShortData()
    {
        yield return [(short)0];
        yield return [(short)42];
        yield return [short.MaxValue];
        yield return [short.MinValue];
    }

    [TestMethod]
    [DynamicData(nameof(ShortData))]
    public void ShortImplicit(short @short)
    {
        Value value = @short;
        value.As<short>().Should().Be(@short);
        value.Type.Should().Be(typeof(short));

        short? source = @short;
        value = source;
        value.As<short?>().Should().Be(source);
        value.Type.Should().Be(typeof(short));
    }

    [TestMethod]
    [DynamicData(nameof(ShortData))]
    public void ShortCreate(short @short)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = Value.Create(@short);
        }

        value.As<short>().Should().Be(@short);
        value.Type.Should().Be(typeof(short));

        short? source = @short;

        using (MemoryWatch.Create)
        {
            value = Value.Create(source);
        }

        value.As<short?>().Should().Be(source);
        value.Type.Should().Be(typeof(short));
    }

    [TestMethod]
    [DynamicData(nameof(ShortData))]
    public void ShortInOut(short @short)
    {
        Value value = @short;
        bool success = value.TryGetValue(out short result);
        success.Should().BeTrue();
        result.Should().Be(@short);

        value.As<short>().Should().Be(@short);
        ((short)value).Should().Be(@short);
    }

    [TestMethod]
    [DynamicData(nameof(ShortData))]
    public void NullableShortInShortOut(short @short)
    {
        short? source = @short;
        Value value = source;

        bool success = value.TryGetValue(out short result);
        success.Should().BeTrue();
        result.Should().Be(@short);

        value.As<short>().Should().Be(@short);

        ((short)value).Should().Be(@short);
    }

    [TestMethod]
    [DynamicData(nameof(ShortData))]
    public void ShortInNullableShortOut(short @short)
    {
        short source = @short;
        Value value = source;
        bool success = value.TryGetValue(out short? result);
        success.Should().BeTrue();
        result.Should().Be(@short);

        ((short?)value).Should().Be(@short);
    }

    [TestMethod]
    [DynamicData(nameof(ShortData))]
    public void BoxedShort(short @short)
    {
        short i = @short;
        object o = i;
        Value value = Value.Create(o);

        value.Type.Should().Be(typeof(short));
        value.TryGetValue(out short result).Should().BeTrue();
        result.Should().Be(@short);
        value.TryGetValue(out short? nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@short);


        short? n = @short;
        o = n;
        value = Value.Create(o);

        value.Type.Should().Be(typeof(short));
        value.TryGetValue(out result).Should().BeTrue();
        result.Should().Be(@short);
        value.TryGetValue(out nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@short);
    }

    [TestMethod]
    public void NullShort()
    {
        short? source = null;
        Value value = source;
        value.Type.Should().BeNull();
        value.As<short?>().Should().Be(source);
        value.As<short?>().HasValue.Should().BeFalse();
    }

    [TestMethod]
    [DynamicData(nameof(ShortData))]
    public void OutAsObject(short @short)
    {
        Value value = @short;
        object o = value.As<object>();
        o.GetType().Should().Be(typeof(short));
        ((short)o).Should().Be(@short);

        short? n = @short;
        value = n;
        o = value.As<object>();
        o.GetType().Should().Be(typeof(short));
        ((short)o).Should().Be(@short);
    }
}
