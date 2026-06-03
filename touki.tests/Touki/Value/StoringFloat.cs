// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class StoringFloat
{
    public static IEnumerable<object[]> FloatData()
    {
        yield return [0f];
        yield return [42f];
        yield return [float.MaxValue];
        yield return [float.MinValue];
        yield return [float.NaN];
        yield return [float.NegativeInfinity];
        yield return [float.PositiveInfinity];
    }

    [TestMethod]
    [DynamicData(nameof(FloatData))]
    public void FloatImplicit(float @float)
    {
        Value value = @float;
        value.As<float>().Should().Be(@float);
        value.Type.Should().Be(typeof(float));

        float? source = @float;
        value = source;
        value.As<float?>().Should().Be(source);
        value.Type.Should().Be(typeof(float));
    }

    [TestMethod]
    [DynamicData(nameof(FloatData))]
    public void FloatCreate(float @float)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = Value.Create(@float);
        }

        value.As<float>().Should().Be(@float);
        value.Type.Should().Be(typeof(float));

        float? source = @float;

        using (MemoryWatch.Create)
        {
            value = Value.Create(source);
        }

        value.As<float?>().Should().Be(source);
        value.Type.Should().Be(typeof(float));
    }

    [TestMethod]
    [DynamicData(nameof(FloatData))]
    public void FloatInOut(float @float)
    {
        Value value = @float;
        bool success = value.TryGetValue(out float result);
        success.Should().BeTrue();
        result.Should().Be(@float);

        value.As<float>().Should().Be(@float);
        ((float)value).Should().Be(@float);
    }

    [TestMethod]
    [DynamicData(nameof(FloatData))]
    public void NullableFloatInFloatOut(float @float)
    {
        float? source = @float;
        Value value = source;

        bool success = value.TryGetValue(out float result);
        success.Should().BeTrue();
        result.Should().Be(@float);

        value.As<float>().Should().Be(@float);

        ((float)value).Should().Be(@float);
    }

    [TestMethod]
    [DynamicData(nameof(FloatData))]
    public void FloatInNullableFloatOut(float @float)
    {
        float source = @float;
        Value value = source;
        bool success = value.TryGetValue(out float? result);
        success.Should().BeTrue();
        result.Should().Be(@float);

        ((float?)value).Should().Be(@float);
    }

    [TestMethod]
    [DynamicData(nameof(FloatData))]
    public void BoxedFloat(float @float)
    {
        float i = @float;
        object o = i;
        Value value = Value.Create(o);

        value.Type.Should().Be(typeof(float));
        value.TryGetValue(out float result).Should().BeTrue();
        result.Should().Be(@float);
        value.TryGetValue(out float? nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@float);


        float? n = @float;
        o = n;
        value = Value.Create(o);

        value.Type.Should().Be(typeof(float));
        value.TryGetValue(out result).Should().BeTrue();
        result.Should().Be(@float);
        value.TryGetValue(out nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@float);
    }

    [TestMethod]
    public void NullFloat()
    {
        float? source = null;
        Value value = source;
        value.Type.Should().BeNull();
        value.As<float?>().Should().Be(source);
        value.As<float?>().HasValue.Should().BeFalse();
    }

    [TestMethod]
    [DynamicData(nameof(FloatData))]
    public void OutAsObject(float @float)
    {
        Value value = @float;
        object o = value.As<object>();
        o.GetType().Should().Be(typeof(float));
        ((float)o).Should().Be(@float);

        float? n = @float;
        value = n;
        o = value.As<object>();
        o.GetType().Should().Be(typeof(float));
        ((float)o).Should().Be(@float);
    }
}
