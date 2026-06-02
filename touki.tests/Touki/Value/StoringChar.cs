// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class StoringChar
{
    public static IEnumerable<char> CharData()
    {
        yield return '!';
        yield return char.MaxValue;
        yield return char.MinValue;
    }

    [Test]
    [MethodDataSource(nameof(CharData))]
    public void CharImplicit(char @char)
    {
        Value value = @char;
        value.As<char>().Should().Be(@char);
        value.Type.Should().Be(typeof(char));

        char? source = @char;
        value = source;
        value.As<char?>().Should().Be(source);
        value.Type.Should().Be(typeof(char));
    }

    [Test]
    [MethodDataSource(nameof(CharData))]
    public void CharCreate(char @char)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = Value.Create(@char);
        }

        value.As<char>().Should().Be(@char);
        value.Type.Should().Be(typeof(char));

        char? source = @char;

        using (MemoryWatch.Create)
        {
            value = Value.Create(source);
        }

        value.As<char?>().Should().Be(source);
        value.Type.Should().Be(typeof(char));
    }

    [Test]
    [MethodDataSource(nameof(CharData))]
    public void CharInOut(char @char)
    {
        Value value = @char;
        bool success = value.TryGetValue(out char result);
        success.Should().BeTrue();
        result.Should().Be(@char);

        value.As<char>().Should().Be(@char);
        ((char)value).Should().Be(@char);
    }

    [Test]
    [MethodDataSource(nameof(CharData))]
    public void NullableCharInCharOut(char @char)
    {
        char? source = @char;
        Value value = source;

        bool success = value.TryGetValue(out char result);
        success.Should().BeTrue();
        result.Should().Be(@char);

        value.As<char>().Should().Be(@char);

        ((char)value).Should().Be(@char);
    }

    [Test]
    [MethodDataSource(nameof(CharData))]
    public void CharInNullableCharOut(char @char)
    {
        char source = @char;
        Value value = source;
        bool success = value.TryGetValue(out char? result);
        success.Should().BeTrue();
        result.Should().Be(@char);

        ((char?)value).Should().Be(@char);
    }

    [Test]
    [MethodDataSource(nameof(CharData))]
    public void BoxedChar(char @char)
    {
        char i = @char;
        object o = i;
        Value value = Value.Create(o);

        value.Type.Should().Be(typeof(char));
        value.TryGetValue(out char result).Should().BeTrue();
        result.Should().Be(@char);
        value.TryGetValue(out char? nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@char);


        char? n = @char;
        o = n;
        value = Value.Create(o);

        value.Type.Should().Be(typeof(char));
        value.TryGetValue(out result).Should().BeTrue();
        result.Should().Be(@char);
        value.TryGetValue(out nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@char);
    }

    [Test]
    public void NullChar()
    {
        char? source = null;
        Value value = source;
        value.Type.Should().BeNull();
        value.As<char?>().Should().Be(source);
        value.As<char?>().HasValue.Should().BeFalse();
    }

    [Test]
    [MethodDataSource(nameof(CharData))]
    public void OutAsObject(char @char)
    {
        Value value = @char;
        object o = value.As<object>();
        o.GetType().Should().Be(typeof(char));
        ((char)o).Should().Be(@char);

        char? n = @char;
        value = n;
        o = value.As<object>();
        o.GetType().Should().Be(typeof(char));
        ((char)o).Should().Be(@char);
    }
}
