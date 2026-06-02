// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class StoringUInt
{
    public static IEnumerable<uint> UIntData()
    {
        yield return 42;
        yield return uint.MaxValue;
        yield return uint.MinValue;
    }

    [Test]
    [MethodDataSource(nameof(UIntData))]
    public void UIntImplicit(uint @uint)
    {
        Value value = @uint;
        value.As<uint>().Should().Be(@uint);
        value.Type.Should().Be(typeof(uint));

        uint? source = @uint;
        value = source;
        value.As<uint?>().Should().Be(source);
        value.Type.Should().Be(typeof(uint));
    }

    [Test]
    [MethodDataSource(nameof(UIntData))]
    public void UIntCreate(uint @uint)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = Value.Create(@uint);
        }

        value.As<uint>().Should().Be(@uint);
        value.Type.Should().Be(typeof(uint));

        uint? source = @uint;

        using (MemoryWatch.Create)
        {
            value = Value.Create(source);
        }

        value.As<uint?>().Should().Be(source);
        value.Type.Should().Be(typeof(uint));
    }

    [Test]
    [MethodDataSource(nameof(UIntData))]
    public void UIntInOut(uint @uint)
    {
        Value value = @uint;
        bool success = value.TryGetValue(out uint result);
        success.Should().BeTrue();
        result.Should().Be(@uint);

        value.As<uint>().Should().Be(@uint);
        ((uint)value).Should().Be(@uint);
    }

    [Test]
    [MethodDataSource(nameof(UIntData))]
    public void NullableUIntInUIntOut(uint @uint)
    {
        uint? source = @uint;
        Value value = source;

        bool success = value.TryGetValue(out uint result);
        success.Should().BeTrue();
        result.Should().Be(@uint);

        value.As<uint>().Should().Be(@uint);

        ((uint)value).Should().Be(@uint);
    }

    [Test]
    [MethodDataSource(nameof(UIntData))]
    public void UIntInNullableUIntOut(uint @uint)
    {
        uint source = @uint;
        Value value = source;
        bool success = value.TryGetValue(out uint? result);
        success.Should().BeTrue();
        result.Should().Be(@uint);

        ((uint?)value).Should().Be(@uint);
    }

    [Test]
    [MethodDataSource(nameof(UIntData))]
    public void BoxedUInt(uint @uint)
    {
        uint i = @uint;
        object o = i;
        Value value = Value.Create(o);

        value.Type.Should().Be(typeof(uint));
        value.TryGetValue(out uint result).Should().BeTrue();
        result.Should().Be(@uint);
        value.TryGetValue(out uint? nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@uint);


        uint? n = @uint;
        o = n;
        value = Value.Create(o);

        value.Type.Should().Be(typeof(uint));
        value.TryGetValue(out result).Should().BeTrue();
        result.Should().Be(@uint);
        value.TryGetValue(out nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@uint);
    }

    [Test]
    public void NullUInt()
    {
        uint? source = null;
        Value value = source;
        value.Type.Should().BeNull();
        value.As<uint?>().Should().Be(source);
        value.As<uint?>().HasValue.Should().BeFalse();
    }

    [Test]
    [MethodDataSource(nameof(UIntData))]
    public void OutAsObject(uint @uint)
    {
        Value value = @uint;
        object o = value.As<object>();
        o.GetType().Should().Be(typeof(uint));
        ((uint)o).Should().Be(@uint);

        uint? n = @uint;
        value = n;
        o = value.As<object>();
        o.GetType().Should().Be(typeof(uint));
        ((uint)o).Should().Be(@uint);
    }
}
