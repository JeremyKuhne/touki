// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class StoringUShort
{
    public static IEnumerable<ushort> UShortData()
    {
        yield return 42;
        yield return ushort.MaxValue;
        yield return ushort.MinValue;
    }

    [Test]
    [MethodDataSource(nameof(UShortData))]
    public void UShortImplicit(ushort @ushort)
    {
        Value value = @ushort;
        value.As<ushort>().Should().Be(@ushort);
        value.Type.Should().Be(typeof(ushort));

        ushort? source = @ushort;
        value = source;
        value.As<ushort?>().Should().Be(source);
        value.Type.Should().Be(typeof(ushort));
    }

    [Test]
    [MethodDataSource(nameof(UShortData))]
    public void UShortCreate(ushort @ushort)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = Value.Create(@ushort);
        }

        value.As<ushort>().Should().Be(@ushort);
        value.Type.Should().Be(typeof(ushort));

        ushort? source = @ushort;

        using (MemoryWatch.Create)
        {
            value = Value.Create(source);
        }

        value.As<ushort?>().Should().Be(source);
        value.Type.Should().Be(typeof(ushort));
    }

    [Test]
    [MethodDataSource(nameof(UShortData))]
    public void UShortInOut(ushort @ushort)
    {
        Value value = @ushort;
        bool success = value.TryGetValue(out ushort result);
        success.Should().BeTrue();
        result.Should().Be(@ushort);

        value.As<ushort>().Should().Be(@ushort);
        ((ushort)value).Should().Be(@ushort);
    }

    [Test]
    [MethodDataSource(nameof(UShortData))]
    public void NullableUShortInUShortOut(ushort @ushort)
    {
        ushort? source = @ushort;
        Value value = source;

        bool success = value.TryGetValue(out ushort result);
        success.Should().BeTrue();
        result.Should().Be(@ushort);

        value.As<ushort>().Should().Be(@ushort);

        ((ushort)value).Should().Be(@ushort);
    }

    [Test]
    [MethodDataSource(nameof(UShortData))]
    public void UShortInNullableUShortOut(ushort @ushort)
    {
        ushort source = @ushort;
        Value value = source;
        bool success = value.TryGetValue(out ushort? result);
        success.Should().BeTrue();
        result.Should().Be(@ushort);

        ((ushort?)value).Should().Be(@ushort);
    }

    [Test]
    [MethodDataSource(nameof(UShortData))]
    public void BoxedUShort(ushort @ushort)
    {
        ushort i = @ushort;
        object o = i;
        Value value = Value.Create(o);

        value.Type.Should().Be(typeof(ushort));
        value.TryGetValue(out ushort result).Should().BeTrue();
        result.Should().Be(@ushort);
        value.TryGetValue(out ushort? nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@ushort);


        ushort? n = @ushort;
        o = n;
        value = Value.Create(o);

        value.Type.Should().Be(typeof(ushort));
        value.TryGetValue(out result).Should().BeTrue();
        result.Should().Be(@ushort);
        value.TryGetValue(out nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@ushort);
    }

    [Test]
    public void NullUShort()
    {
        ushort? source = null;
        Value value = source;
        value.Type.Should().BeNull();
        value.As<ushort?>().Should().Be(source);
        value.As<ushort?>().HasValue.Should().BeFalse();
    }

    [Test]
    [MethodDataSource(nameof(UShortData))]
    public void OutAsObject(ushort @ushort)
    {
        Value value = @ushort;
        object o = value.As<object>();
        o.GetType().Should().Be(typeof(ushort));
        ((ushort)o).Should().Be(@ushort);

        ushort? n = @ushort;
        value = n;
        o = value.As<object>();
        o.GetType().Should().Be(typeof(ushort));
        ((ushort)o).Should().Be(@ushort);
    }
}
