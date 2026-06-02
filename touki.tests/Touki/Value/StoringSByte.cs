// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class StoringSByte
{
    public static IEnumerable<sbyte> SByteData()
    {
        yield return 0;
        yield return 42;
        yield return sbyte.MaxValue;
        yield return sbyte.MinValue;
    }

    [Test]
    [MethodDataSource(nameof(SByteData))]
    public void SByteImplicit(sbyte @sbyte)
    {
        Value value = @sbyte;
        value.As<sbyte>().Should().Be(@sbyte);
        value.Type.Should().Be(typeof(sbyte));

        sbyte? source = @sbyte;
        value = source;
        value.As<sbyte?>().Should().Be(source);
        value.Type.Should().Be(typeof(sbyte));
    }

    [Test]
    [MethodDataSource(nameof(SByteData))]
    public void SByteCreate(sbyte @sbyte)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = Value.Create(@sbyte);
        }

        value.As<sbyte>().Should().Be(@sbyte);
        value.Type.Should().Be(typeof(sbyte));

        sbyte? source = @sbyte;

        using (MemoryWatch.Create)
        {
            value = Value.Create(source);
        }

        value.As<sbyte?>().Should().Be(source);
        value.Type.Should().Be(typeof(sbyte));
    }

    [Test]
    [MethodDataSource(nameof(SByteData))]
    public void SByteInOut(sbyte @sbyte)
    {
        Value value = @sbyte;
        bool success = value.TryGetValue(out sbyte result);
        success.Should().BeTrue();
        result.Should().Be(@sbyte);

        value.As<sbyte>().Should().Be(@sbyte);
        ((sbyte)value).Should().Be(@sbyte);
    }

    [Test]
    [MethodDataSource(nameof(SByteData))]
    public void NullableSByteInSByteOut(sbyte @sbyte)
    {
        sbyte? source = @sbyte;
        Value value = source;

        bool success = value.TryGetValue(out sbyte result);
        success.Should().BeTrue();
        result.Should().Be(@sbyte);

        value.As<sbyte>().Should().Be(@sbyte);

        ((sbyte)value).Should().Be(@sbyte);
    }

    [Test]
    [MethodDataSource(nameof(SByteData))]
    public void SByteInNullableSByteOut(sbyte @sbyte)
    {
        sbyte source = @sbyte;
        Value value = source;
        bool success = value.TryGetValue(out sbyte? result);
        success.Should().BeTrue();
        result.Should().Be(@sbyte);

        ((sbyte?)value).Should().Be(@sbyte);
    }

    [Test]
    public void NullSByte()
    {
        sbyte? source = null;
        Value value = source;
        value.Type.Should().BeNull();
        value.As<sbyte?>().Should().Be(source);
        value.As<sbyte?>().HasValue.Should().BeFalse();
    }

    [Test]
    [MethodDataSource(nameof(SByteData))]
    public void OutAsObject(sbyte @sbyte)
    {
        Value value = @sbyte;
        object o = value.As<object>();
        o.GetType().Should().Be(typeof(sbyte));
        ((sbyte)o).Should().Be(@sbyte);

        sbyte? n = @sbyte;
        value = n;
        o = value.As<object>();
        o.GetType().Should().Be(typeof(sbyte));
        ((sbyte)o).Should().Be(@sbyte);
    }
}
