// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class StoringLong
{
    public static IEnumerable<object[]> LongData()
    {
        yield return [0L];
        yield return [42L];
        yield return [long.MaxValue];
        yield return [long.MinValue];
    }

    [TestMethod]
    [DynamicData(nameof(LongData))]
    public void LongImplicit(long @long)
    {
        Value value = @long;
        value.As<long>().Should().Be(@long);
        value.Type.Should().Be(typeof(long));

        long? source = @long;
        value = source;
        value.As<long>().Should().Be(source);
        value.Type.Should().Be(typeof(long));
    }

    [TestMethod]
    [DynamicData(nameof(LongData))]
    public void LongCreate(long @long)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = Value.Create(@long);
        }

        value.As<long>().Should().Be(@long);
        value.Type.Should().Be(typeof(long));

        long? source = @long;

        using (MemoryWatch.Create)
        {
            value = Value.Create(source);
        }

        value.As<long?>().Should().Be(source);
        value.Type.Should().Be(typeof(long));
    }

    [TestMethod]
    [DynamicData(nameof(LongData))]
    public void LongInOut(long @long)
    {
        Value value = @long;
        bool success = value.TryGetValue(out long result);
        success.Should().BeTrue();
        result.Should().Be(@long);

        value.As<long>().Should().Be(@long);
        ((long)value).Should().Be(@long);
    }

    [TestMethod]
    [DynamicData(nameof(LongData))]
    public void NullableLongInLongOut(long @long)
    {
        long? source = @long;
        Value value = source;

        bool success = value.TryGetValue(out long result);
        success.Should().BeTrue();
        result.Should().Be(@long);

        value.As<long>().Should().Be(@long);

        ((long)value).Should().Be(@long);
    }

    [TestMethod]
    [DynamicData(nameof(LongData))]
    public void LongInNullableLongOut(long @long)
    {
        long source = @long;
        Value value = source;
        bool success = value.TryGetValue(out long? result);
        success.Should().BeTrue();
        result.Should().Be(@long);

        ((long?)value).Should().Be(@long);
    }

    [TestMethod]
    [DynamicData(nameof(LongData))]
    public void BoxedLong(long @long)
    {
        long i = @long;
        object o = i;
        Value value = Value.Create(o);

        value.Type.Should().Be(typeof(long));
        value.TryGetValue(out long result).Should().BeTrue();
        result.Should().Be(@long);
        value.TryGetValue(out long? nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@long);


        long? n = @long;
        o = n;
        value = Value.Create(o);

        value.Type.Should().Be(typeof(long));
        value.TryGetValue(out result).Should().BeTrue();
        result.Should().Be(@long);
        value.TryGetValue(out nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@long);
    }

    [TestMethod]
    public void NullLong()
    {
        long? source = null;
        Value value = source;
        value.Type.Should().BeNull();
        value.As<long?>().Should().Be(source);
        value.As<long?>().HasValue.Should().BeFalse();
    }

    [TestMethod]
    [DynamicData(nameof(LongData))]
    public void OutAsObject(long @long)
    {
        Value value = @long;
        object o = value.As<object>();
        o.GetType().Should().Be(typeof(long));
        ((long)o).Should().Be(@long);

        long? n = @long;
        value = n;
        o = value.As<object>();
        o.GetType().Should().Be(typeof(long));
        ((long)o).Should().Be(@long);
    }
}
