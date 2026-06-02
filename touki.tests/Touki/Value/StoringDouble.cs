// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class StoringDouble
{
    public static IEnumerable<double> DoubleData()
    {
        yield return 0d;
        yield return 42d;
        yield return double.MaxValue;
        yield return double.MinValue;
        yield return double.NaN;
        yield return double.NegativeInfinity;
        yield return double.PositiveInfinity;
    }

    [Test]
    [MethodDataSource(nameof(DoubleData))]
    public void DoubleImplicit(double @double)
    {
        Value value = @double;
        value.As<double>().Should().Be(@double);
        value.Type.Should().Be(typeof(double));

        double? source = @double;
        value = source;
        value.As<double?>().Should().Be(source);
        value.Type.Should().Be(typeof(double));
    }

    [Test]
    [MethodDataSource(nameof(DoubleData))]
    public void DoubleCreate(double @double)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = Value.Create(@double);
        }

        value.As<double>().Should().Be(@double);
        value.Type.Should().Be(typeof(double));

        double? source = @double;

        using (MemoryWatch.Create)
        {
            value = Value.Create(source);
        }

        value.As<double?>().Should().Be(source);
        value.Type.Should().Be(typeof(double));
    }

    [Test]
    [MethodDataSource(nameof(DoubleData))]
    public void DoubleInOut(double @double)
    {
        Value value = @double;
        bool success = value.TryGetValue(out double result);
        success.Should().BeTrue();
        result.Should().Be(@double);

        value.As<double>().Should().Be(@double);
        ((double)value).Should().Be(@double);
    }

    [Test]
    [MethodDataSource(nameof(DoubleData))]
    public void NullableDoubleInDoubleOut(double @double)
    {
        double? source = @double;
        Value value = source;

        bool success = value.TryGetValue(out double result);
        success.Should().BeTrue();
        result.Should().Be(@double);

        value.As<double>().Should().Be(@double);

        ((double)value).Should().Be(@double);
    }

    [Test]
    [MethodDataSource(nameof(DoubleData))]
    public void DoubleInNullableDoubleOut(double @double)
    {
        double source = @double;
        Value value = source;
        bool success = value.TryGetValue(out double? result);
        success.Should().BeTrue();
        result.Should().Be(@double);

        ((double)value).Should().Be(@double);
    }

    [Test]
    [MethodDataSource(nameof(DoubleData))]
    public void BoxedDouble(double @double)
    {
        double i = @double;
        object o = i;
        Value value = Value.Create(o);

        value.Type.Should().Be(typeof(double));
        value.TryGetValue(out double result).Should().BeTrue();
        result.Should().Be(@double);
        value.TryGetValue(out double? nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@double);


        double? n = @double;
        o = n;
        value = Value.Create(o);

        value.Type.Should().Be(typeof(double));
        value.TryGetValue(out result).Should().BeTrue();
        result.Should().Be(@double);
        value.TryGetValue(out nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@double);
    }

    [Test]
    public void NullDouble()
    {
        double? source = null;
        Value value = source;
        value.Type.Should().BeNull();
        value.As<double?>().Should().Be(source);
        value.As<double?>().HasValue.Should().BeFalse();
    }

    [Test]
    [MethodDataSource(nameof(DoubleData))]
    public void OutAsObject(double @double)
    {
        Value value = @double;
        object o = value.As<object>();
        o.GetType().Should().Be(typeof(double));
        ((double)o).Should().Be(@double);

        double? n = @double;
        value = n;
        o = value.As<object>();
        o.GetType().Should().Be(typeof(double));
        ((double)o).Should().Be(@double);
    }
}
