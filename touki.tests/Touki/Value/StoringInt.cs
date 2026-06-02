// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class StoringInt
{
    public static IEnumerable<int> IntData()
    {
        yield return 0;
        yield return 42;
        yield return int.MaxValue;
        yield return int.MinValue;
    }

    [Test]
    [MethodDataSource(nameof(IntData))]
    public void IntImplicit(int @int)
    {
        Value value = @int;
        value.As<int>().Should().Be(@int);
        value.Type.Should().Be(typeof(int));

        int? source = @int;
        value = source;
        value.As<int?>().Should().Be(source);
        value.Type.Should().Be(typeof(int));
    }

    [Test]
    [MethodDataSource(nameof(IntData))]
    public void IntCreate(int @int)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = Value.Create(@int);
        }

        value.As<int>().Should().Be(@int);
        value.Type.Should().Be(typeof(int));

        int? source = @int;

        using (MemoryWatch.Create)
        {
            value = Value.Create(source);
        }

        value.As<int?>().Should().Be(source);
        value.Type.Should().Be(typeof(int));
    }

    [Test]
    [MethodDataSource(nameof(IntData))]
    public void IntInOut(int @int)
    {
        Value value = @int;
        bool success = value.TryGetValue(out int result);
        success.Should().BeTrue();
        result.Should().Be(@int);

        value.As<int>().Should().Be(@int);
        ((int)value).Should().Be(@int);
    }

    [Test]
    [MethodDataSource(nameof(IntData))]
    public void NullableIntInIntOut(int @int)
    {
        int? source = @int;
        Value value = source;

        bool success = value.TryGetValue(out int result);
        success.Should().BeTrue();
        result.Should().Be(@int);

        value.As<int>().Should().Be(@int);

        ((int)value).Should().Be(@int);
    }

    [Test]
    [MethodDataSource(nameof(IntData))]
    public void IntInNullableIntOut(int @int)
    {
        int source = @int;
        Value value = source;
        value.TryGetValue(out int? result).Should().BeTrue();
        result.Should().Be(@int);

        ((int?)value).Should().Be(@int);
    }

    [Test]
    [MethodDataSource(nameof(IntData))]
    public void BoxedInt(int @int)
    {
        int i = @int;
        object o = i;
        Value value = Value.Create(o);

        value.Type.Should().Be(typeof(int));
        value.TryGetValue(out int result).Should().BeTrue();
        result.Should().Be(@int);
        value.TryGetValue(out int? nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@int);


        int? n = @int;
        o = n;
        value = Value.Create(o);

        value.Type.Should().Be(typeof(int));
        value.TryGetValue(out result).Should().BeTrue();
        result.Should().Be(@int);
        value.TryGetValue(out nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@int);
    }

    [Test]
    public void NullInt()
    {
        int? source = null;
        Value value = source;
        value.Type.Should().BeNull();
        value.As<int?>().Should().Be(source);
        value.As<int?>().HasValue.Should().BeFalse();
    }

    [Test]
    [MethodDataSource(nameof(IntData))]
    public void OutAsObject(int @int)
    {
        Value value = @int;
        object o = value.As<object>();
        o.GetType().Should().Be(typeof(int));
        ((int)o).Should().Be(@int);

        int? n = @int;
        value = n;
        o = value.As<object>();
        o.GetType().Should().Be(typeof(int));
        ((int)o).Should().Be(@int);
    }
}
