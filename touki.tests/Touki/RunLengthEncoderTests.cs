// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class RunLengthEncoderTests
{
    [Fact]
    public void RunLengthEncoder_TryEncode()
    {
        ReadOnlySpan<byte> data = [1, 1, 1, 2, 2, 3, 3, 3, 3];
        Span<byte> encoded = new byte[RunLengthEncoder.GetEncodedLength(data)];
        RunLengthEncoder.TryEncode(data, encoded, out int written).Should().BeTrue();
        written.Should().Be(6);
        encoded.ToArray().Should().BeEquivalentTo([3, 1, 2, 2, 4, 3]);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 2)]
    [InlineData(255, 2)]
    [InlineData(256, 4)]
    public void RunLengthEncoder_GetEncodedLength(int count, int expectedLength)
    {
        Span<byte> data = new byte[count];
        Span<byte> encoded = new byte[RunLengthEncoder.GetEncodedLength(data)];
        RunLengthEncoder.TryEncode(data, encoded, out int written).Should().BeTrue();
        written.Should().Be(expectedLength);
        encoded.Length.Should().Be(expectedLength);
    }

    [Fact]
    public void RunLengthEncoder_RoundTrip()
    {
        ReadOnlySpan<byte> data = [1, 1, 1, 2, 2, 3, 3, 3, 3];
        Span<byte> encoded = new byte[RunLengthEncoder.GetEncodedLength(data)];
        RunLengthEncoder.TryEncode(data, encoded, out int written).Should().BeTrue();
        written.Should().Be(6);

        Span<byte> decoded = new byte[RunLengthEncoder.GetDecodedLength(encoded)];
        RunLengthEncoder.TryDecode(encoded, decoded).Should().BeTrue();
        decoded.ToArray().Should().BeEquivalentTo(data.ToArray());
    }

    [Fact]
    public void RunLengthEncoder_GetEncodedLength_EmptyData()
    {
        ReadOnlySpan<byte> data = [];
        int length = RunLengthEncoder.GetEncodedLength(data);
        length.Should().Be(0);
    }

    [Fact]
    public void RunLengthEncoder_GetEncodedLength_SingleByte()
    {
        ReadOnlySpan<byte> data = [42];
        int length = RunLengthEncoder.GetEncodedLength(data);
        length.Should().Be(2); // 1 byte count + 1 byte value
    }

    [Fact]
    public void RunLengthEncoder_GetEncodedLength_NoRepeats()
    {
        ReadOnlySpan<byte> data = [1, 2, 3, 4, 5];
        int length = RunLengthEncoder.GetEncodedLength(data);
        length.Should().Be(10); // 5 values * 2 bytes each (count + value)
    }

    [Fact]
    public void RunLengthEncoder_GetEncodedLength_AllSameValue()
    {
        ReadOnlySpan<byte> data = [7, 7, 7, 7, 7];
        int length = RunLengthEncoder.GetEncodedLength(data);
        length.Should().Be(2); // Single run of 5 * value 7
    }

    [Fact]
    public void RunLengthEncoder_GetEncodedLength_LargeRun()
    {
        // Test run longer than 255 (max byte value)
        Span<byte> data = new byte[300];
        data.Fill(42);

        int length = RunLengthEncoder.GetEncodedLength(data);
        length.Should().Be(4); // 255 + 45 = two runs
    }

    [Fact]
    public void RunLengthEncoder_GetDecodedLength_EmptyData()
    {
        ReadOnlySpan<byte> encoded = [];
        int length = RunLengthEncoder.GetDecodedLength(encoded);
        length.Should().Be(0);
    }

    [Fact]
    public void RunLengthEncoder_GetDecodedLength_SingleRun()
    {
        ReadOnlySpan<byte> encoded = [5, 42]; // 5 times value 42
        int length = RunLengthEncoder.GetDecodedLength(encoded);
        length.Should().Be(5);
    }

    [Fact]
    public void RunLengthEncoder_GetDecodedLength_MultipleRuns()
    {
        ReadOnlySpan<byte> encoded = [3, 1, 2, 2, 4, 3]; // 3*1 + 2*2 + 4*3 = 3+2+4=9
        int length = RunLengthEncoder.GetDecodedLength(encoded);
        length.Should().Be(9);
    }

    [Fact]
    public void RunLengthEncoder_GetDecodedLength_OddLength()
    {
        // Invalid encoded data with odd length - processes all bytes in pairs
        ReadOnlySpan<byte> encoded = [3, 1, 2]; // Missing value for count 2
        int length = RunLengthEncoder.GetDecodedLength(encoded);
        length.Should().Be(5); // Processes [3, 1] as pair (3) + [2] as single count (2) = 5
    }

    [Fact]
    public void RunLengthEncoder_TryEncode_EmptyData()
    {
        ReadOnlySpan<byte> data = [];
        Span<byte> encoded = new byte[10];

        RunLengthEncoder.TryEncode(data, encoded, out int written).Should().BeTrue();
        written.Should().Be(0);
    }

    [Fact]
    public void RunLengthEncoder_TryEncode_SingleByte()
    {
        ReadOnlySpan<byte> data = [42];
        Span<byte> encoded = new byte[2];

        RunLengthEncoder.TryEncode(data, encoded, out int written).Should().BeTrue();
        written.Should().Be(2);
        encoded.ToArray().Should().BeEquivalentTo([1, 42]);
    }

    [Fact]
    public void RunLengthEncoder_TryEncode_NoRepeats()
    {
        ReadOnlySpan<byte> data = [1, 2, 3];
        Span<byte> encoded = new byte[6];

        RunLengthEncoder.TryEncode(data, encoded, out int written).Should().BeTrue();
        written.Should().Be(6);
        encoded.ToArray().Should().BeEquivalentTo([1, 1, 1, 2, 1, 3]);
    }

    [Fact]
    public void RunLengthEncoder_TryEncode_LargeRun()
    {
        // Test run of exactly 255
        Span<byte> data = new byte[255];
        data.Fill(99);
        Span<byte> encoded = new byte[2];

        RunLengthEncoder.TryEncode(data, encoded, out int written).Should().BeTrue();
        written.Should().Be(2);
        encoded.ToArray().Should().BeEquivalentTo([255, 99]);
    }

    [Fact]
    public void RunLengthEncoder_TryEncode_VeryLargeRun()
    {
        // Test run longer than 255
        Span<byte> data = new byte[300];
        data.Fill(77);
        Span<byte> encoded = new byte[4];

        RunLengthEncoder.TryEncode(data, encoded, out int written).Should().BeTrue();
        written.Should().Be(4);
        encoded.ToArray().Should().BeEquivalentTo([255, 77, 45, 77]); // 255 + 45 = 300
    }

    [Fact]
    public void RunLengthEncoder_TryEncode_BufferTooSmall()
    {
        ReadOnlySpan<byte> data = [1, 1, 1];
        Span<byte> encoded = new byte[1]; // Need 2 bytes but only have 1

        RunLengthEncoder.TryEncode(data, encoded, out int written).Should().BeFalse();
        written.Should().Be(1); // Partial write - count byte written but value byte failed
    }

    [Fact]
    public void RunLengthEncoder_TryEncode_ExactBufferSize()
    {
        ReadOnlySpan<byte> data = [1, 1, 2, 2, 2];
        int requiredSize = RunLengthEncoder.GetEncodedLength(data);
        Span<byte> encoded = new byte[requiredSize];

        RunLengthEncoder.TryEncode(data, encoded, out int written).Should().BeTrue();
        written.Should().Be(requiredSize);
        encoded.ToArray().Should().BeEquivalentTo([2, 1, 3, 2]);
    }

    [Fact]
    public void RunLengthEncoder_TryDecode_EmptyData()
    {
        ReadOnlySpan<byte> encoded = [];
        Span<byte> decoded = new byte[10];

        RunLengthEncoder.TryDecode(encoded, decoded).Should().BeTrue();
    }

    [Fact]
    public void RunLengthEncoder_TryDecode_SingleRun()
    {
        ReadOnlySpan<byte> encoded = [3, 42];
        Span<byte> decoded = new byte[3];

        RunLengthEncoder.TryDecode(encoded, decoded).Should().BeTrue();
        decoded.ToArray().Should().BeEquivalentTo([42, 42, 42]);
    }

    [Fact]
    public void RunLengthEncoder_TryDecode_MultipleRuns()
    {
        ReadOnlySpan<byte> encoded = [2, 1, 3, 2, 1, 3];
        Span<byte> decoded = new byte[6];

        RunLengthEncoder.TryDecode(encoded, decoded).Should().BeTrue();
        decoded.ToArray().Should().BeEquivalentTo([1, 1, 2, 2, 2, 3]);
    }

    [Fact]
    public void RunLengthEncoder_TryDecode_BufferTooSmall()
    {
        ReadOnlySpan<byte> encoded = [5, 42]; // Needs 5 bytes
        Span<byte> decoded = new byte[3]; // Only 3 bytes available

        RunLengthEncoder.TryDecode(encoded, decoded).Should().BeFalse();
    }

    [Fact]
    public void RunLengthEncoder_TryDecode_InvalidEncodedData_OddLength()
    {
        ReadOnlySpan<byte> encoded = [3, 42, 2]; // Missing value for count 2
        Span<byte> decoded = new byte[10];

        RunLengthEncoder.TryDecode(encoded, decoded).Should().BeFalse();
    }

    [Fact]
    public void RunLengthEncoder_TryDecode_ZeroCount()
    {
        ReadOnlySpan<byte> encoded = [0, 42, 3, 99]; // Zero count followed by normal run
        Span<byte> decoded = new byte[10];

        RunLengthEncoder.TryDecode(encoded, decoded).Should().BeTrue();
        // Should write 0 bytes for first run, then 3 bytes of 99
        decoded[..3].ToArray().Should().BeEquivalentTo([99, 99, 99]);
    }

    [Fact]
    public void RunLengthEncoder_TryDecode_ExactBufferSize()
    {
        ReadOnlySpan<byte> encoded = [2, 1, 3, 2];
        int requiredSize = RunLengthEncoder.GetDecodedLength(encoded);
        Span<byte> decoded = new byte[requiredSize];

        RunLengthEncoder.TryDecode(encoded, decoded).Should().BeTrue();
        decoded.ToArray().Should().BeEquivalentTo([1, 1, 2, 2, 2]);
    }

    [Fact]
    public void RunLengthEncoder_RoundTrip_ComplexData()
    {
        ReadOnlySpan<byte> originalData = [1, 1, 1, 2, 3, 3, 4, 4, 4, 4, 4, 5];

        // Encode
        Span<byte> encoded = new byte[RunLengthEncoder.GetEncodedLength(originalData)];
        RunLengthEncoder.TryEncode(originalData, encoded, out int _).Should().BeTrue();

        // Decode
        Span<byte> decoded = new byte[RunLengthEncoder.GetDecodedLength(encoded)];
        RunLengthEncoder.TryDecode(encoded, decoded).Should().BeTrue();

        // Verify round trip
        decoded.ToArray().Should().BeEquivalentTo(originalData.ToArray());
    }

    [Fact]
    public void RunLengthEncoder_RoundTrip_LargeData()
    {
        // Create data with various run lengths
        List<byte> dataList = [];

        // Add runs of different lengths
        for (byte value = 1; value <= 10; value++)
        {
            for (int i = 0; i < value * 20; i++) // Varying run lengths
            {
                dataList.Add(value);
            }
        }

        ReadOnlySpan<byte> originalData = dataList.ToArray();

        // Encode
        Span<byte> encoded = new byte[RunLengthEncoder.GetEncodedLength(originalData)];
        RunLengthEncoder.TryEncode(originalData, encoded, out int _).Should().BeTrue();

        // Decode
        Span<byte> decoded = new byte[RunLengthEncoder.GetDecodedLength(encoded)];
        RunLengthEncoder.TryDecode(encoded, decoded).Should().BeTrue();

        // Verify round trip
        decoded.ToArray().Should().BeEquivalentTo(originalData.ToArray());
    }

    [Fact]
    public void RunLengthEncoder_EdgeCase_MaxByteValue()
    {
        ReadOnlySpan<byte> data = [255, 255, 255];

        Span<byte> encoded = new byte[RunLengthEncoder.GetEncodedLength(data)];
        RunLengthEncoder.TryEncode(data, encoded, out int _).Should().BeTrue();

        Span<byte> decoded = new byte[RunLengthEncoder.GetDecodedLength(encoded)];
        RunLengthEncoder.TryDecode(encoded, decoded).Should().BeTrue();

        decoded.ToArray().Should().BeEquivalentTo([255, 255, 255]);
    }

    [Fact]
    public void RunLengthEncoder_EdgeCase_AlternatingPattern()
    {
        ReadOnlySpan<byte> data = [1, 2, 1, 2, 1, 2];

        Span<byte> encoded = new byte[RunLengthEncoder.GetEncodedLength(data)];
        RunLengthEncoder.TryEncode(data, encoded, out int written).Should().BeTrue();

        // Should encode as 6 separate runs since no consecutive values
        written.Should().Be(12); // 6 runs * 2 bytes each

        Span<byte> decoded = new byte[RunLengthEncoder.GetDecodedLength(encoded)];
        RunLengthEncoder.TryDecode(encoded, decoded).Should().BeTrue();

        decoded.ToArray().Should().BeEquivalentTo(data.ToArray());
    }

    [Fact]
    public void RunLengthEncoder_Consistency_GetEncodedLength()
    {
        // Verify that GetEncodedLength matches actual encoding output
        ReadOnlySpan<byte> data = [1, 1, 2, 2, 2, 3, 4, 4, 4, 4];

        int predictedLength = RunLengthEncoder.GetEncodedLength(data);
        Span<byte> encoded = new byte[predictedLength + 10]; // Extra space

        RunLengthEncoder.TryEncode(data, encoded, out int actualLength).Should().BeTrue();
        actualLength.Should().Be(predictedLength);
    }

    [Fact]
    public void RunLengthEncoder_Consistency_GetDecodedLength()
    {
        // Verify that GetDecodedLength matches actual decoding output
        ReadOnlySpan<byte> encoded = [3, 1, 2, 2, 4, 3];

        int predictedLength = RunLengthEncoder.GetDecodedLength(encoded);
        Span<byte> decoded = new byte[predictedLength];

        RunLengthEncoder.TryDecode(encoded, decoded).Should().BeTrue();
        // All bytes should be used (no extra space needed)
        decoded.Length.Should().Be(predictedLength);
    }
}
