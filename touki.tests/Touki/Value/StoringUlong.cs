// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class StoringULong
{
    public static IEnumerable<ulong> ULongData()
    {
        yield return 42;
        yield return ulong.MaxValue;
        yield return ulong.MinValue;
    }

    [Test]
    [MethodDataSource(nameof(ULongData))]
    public void ULongImplicit(ulong @ulong)
    {
        Value value = @ulong;
        value.As<ulong>().Should().Be(@ulong);
        value.Type.Should().Be(typeof(ulong));

        ulong? source = @ulong;
        value = source;
        value.As<ulong?>().Should().Be(source);
        value.Type.Should().Be(typeof(ulong));
    }

    [Test]
    [MethodDataSource(nameof(ULongData))]
    public void ULongCreate(ulong @ulong)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = Value.Create(@ulong);
        }

        value.As<ulong>().Should().Be(@ulong);
        value.Type.Should().Be(typeof(ulong));

        ulong? source = @ulong;

        using (MemoryWatch.Create)
        {
            value = Value.Create(source);
        }

        value.As<ulong?>().Should().Be(source);
        value.Type.Should().Be(typeof(ulong));
    }

    [Test]
    [MethodDataSource(nameof(ULongData))]
    public void ULongInOut(ulong @ulong)
    {
        Value value = @ulong;
        bool success = value.TryGetValue(out ulong result);
        success.Should().BeTrue();
        result.Should().Be(@ulong);

        value.As<ulong>().Should().Be(@ulong);
        ((ulong)value).Should().Be(@ulong);
    }

    [Test]
    [MethodDataSource(nameof(ULongData))]
    public void NullableULongInULongOut(ulong @ulong)
    {
        ulong? source = @ulong;
        Value value = source;

        bool success = value.TryGetValue(out ulong result);
        success.Should().BeTrue();
        result.Should().Be(@ulong);

        value.As<ulong>().Should().Be(@ulong);

        ((ulong)value).Should().Be(@ulong);
    }

    [Test]
    [MethodDataSource(nameof(ULongData))]
    public void ULongInNullableULongOut(ulong @ulong)
    {
        ulong source = @ulong;
        Value value = source;
        bool success = value.TryGetValue(out ulong? result);
        success.Should().BeTrue();
        result.Should().Be(@ulong);

        ((ulong?)value).Should().Be(@ulong);
    }

    [Test]
    [MethodDataSource(nameof(ULongData))]
    public void BoxedULong(ulong @ulong)
    {
        ulong i = @ulong;
        object o = i;
        Value value = Value.Create(o);

        value.Type.Should().Be(typeof(ulong));
        value.TryGetValue(out ulong result).Should().BeTrue();
        result.Should().Be(@ulong);
        value.TryGetValue(out ulong? nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@ulong);


        ulong? n = @ulong;
        o = n;
        value = Value.Create(o);

        value.Type.Should().Be(typeof(ulong));
        value.TryGetValue(out result).Should().BeTrue();
        result.Should().Be(@ulong);
        value.TryGetValue(out nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@ulong);
    }

    [Test]
    public void NullULong()
    {
        ulong? source = null;
        Value value = source;
        value.Type.Should().BeNull();
        value.As<ulong?>().Should().Be(source);
        value.As<ulong?>().HasValue.Should().BeFalse();
    }

    [Test]
    [MethodDataSource(nameof(ULongData))]
    public void OutAsObject(ulong @ulong)
    {
        Value value = @ulong;
        object o = value.As<object>();
        o.GetType().Should().Be(typeof(ulong));
        ((ulong)o).Should().Be(@ulong);

        ulong? n = @ulong;
        value = n;
        o = value.As<object>();
        o.GetType().Should().Be(typeof(ulong));
        ((ulong)o).Should().Be(@ulong);
    }
}
