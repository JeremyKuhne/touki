// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class StoringBoolean
{
    public static IEnumerable<bool> BoolData()
    {
        yield return true;
        yield return false;
    }

    [Test]
    [MethodDataSource(nameof(BoolData))]
    public void BooleanImplicit(bool @bool)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = @bool;
        }

        value.As<bool>().Should().Be(@bool);
        value.Type.Should().Be(typeof(bool));

        bool? source = @bool;
        using (MemoryWatch.Create)
        {
            value = source;
        }
        value.As<bool?>().Should().Be(source);
        value.Type.Should().Be(typeof(bool));
    }

    [Test]
    [MethodDataSource(nameof(BoolData))]
    public void BooleanCreate(bool @bool)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = Value.Create(@bool);
        }

        value.As<bool>().Should().Be(@bool);
        value.Type.Should().Be(typeof(bool));

        bool? source = @bool;

        using (MemoryWatch.Create)
        {
            value = Value.Create(source);
        }

        value.As<bool?>().Should().Be(source);
        value.Type.Should().Be(typeof(bool));
    }

    [Test]
    [MethodDataSource(nameof(BoolData))]
    public void BooleanInOut(bool @bool)
    {
        Value value;
        bool success;
        bool result;

        using (MemoryWatch.Create)
        {
            value = @bool;
            success = value.TryGetValue(out result);
        }

        success.Should().BeTrue();
        result.Should().Be(@bool);

        value.As<bool>().Should().Be(@bool);
        ((bool)value).Should().Be(@bool);
    }

    [Test]
    [MethodDataSource(nameof(BoolData))]
    public void NullableBooleanInBooleanOut(bool @bool)
    {
        bool? source = @bool;
        Value value;
        bool success;
        bool result;

        using (MemoryWatch.Create)
        {
            value = source;
            success = value.TryGetValue(out result);
        }

        success.Should().BeTrue();
        result.Should().Be(@bool);

        value.As<bool>().Should().Be(@bool);

        ((bool)value).Should().Be(@bool);
    }

    [Test]
    [MethodDataSource(nameof(BoolData))]
    public void BooleanInNullableBooleanOut(bool @bool)
    {
        bool source = @bool;
        Value value = source;
        bool success = value.TryGetValue(out bool? result);
        success.Should().BeTrue();
        result.Should().Be(@bool);

        ((bool?)value).Should().Be(@bool);
    }

    [Test]
    [MethodDataSource(nameof(BoolData))]
    public void BoxedBoolean(bool @bool)
    {
        bool i = @bool;
        object o = i;
        Value value = Value.Create(o);

        value.Type.Should().Be(typeof(bool));
        value.TryGetValue(out bool result).Should().BeTrue();
        result.Should().Be(@bool);
        value.TryGetValue(out bool? nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@bool);


        bool? n = @bool;
        o = n;
        value = Value.Create(o);

        value.Type.Should().Be(typeof(bool));
        value.TryGetValue(out result).Should().BeTrue();
        result.Should().Be(@bool);
        value.TryGetValue(out nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@bool);
    }

    [Test]
    public void NullBoolean()
    {
        bool? source = null;
        Value value;

        using (MemoryWatch.Create)
        {
            value = source;
        }

        value.Type.Should().BeNull();
        value.As<bool?>().Should().Be(source);
        value.As<bool?>().HasValue.Should().BeFalse();
    }

    [Test]
    [MethodDataSource(nameof(BoolData))]
    public void OutAsObject(bool @bool)
    {
        Value value = @bool;
        object o = value.As<object>();
        o.GetType().Should().Be(typeof(bool));
        ((bool)o).Should().Be(@bool);

        bool? n = @bool;
        value = n;
        o = value.As<object>();
        o.GetType().Should().Be(typeof(bool));
        ((bool)o).Should().Be(@bool);
    }
}
