// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

[TestClass]
public class IndexedStringResourceTableTests
{
    private sealed class StubStringResourceReader(ResourceTypeCode typeCode) : IStringResourceReader
    {
        private int _lookupCount;

        public int DisposeCount { get; private set; }

        public int LookupCount => Volatile.Read(ref _lookupCount);

        public int ResourceCount => 1;

        public ResourceTypeCode GetResourceTypeCode(int index) => typeCode;

        public string GetResourceName(int index) => "Greeting";

        public string GetString(int index) => "Hello";

        public string? Lookup(string name)
        {
            Interlocked.Increment(ref _lookupCount);
            return "Hello";
        }

        public void Dispose() => DisposeCount++;
    }

    [TestMethod]
    public void Create_NullResource_DisposesReaderAndThrows()
    {
        StubStringResourceReader reader = new(ResourceTypeCode.Null);
        try
        {
            Action action = () => IndexedStringResourceTable.Create(
                reader,
                StringResourceManagerOptions.None);

            action.Should().Throw<BadImageFormatException>();
            reader.DisposeCount.Should().Be(1);
        }
        finally
        {
            if (reader.DisposeCount == 0)
            {
                reader.Dispose();
            }
        }
    }

    [TestMethod]
    public void Create_NonStringResource_DisposesReaderAndThrows()
    {
        StubStringResourceReader reader = new(ResourceTypeCode.Int32);
        try
        {
            Action action = () => IndexedStringResourceTable.Create(
                reader,
                StringResourceManagerOptions.None);

            action.Should().Throw<NotSupportedException>();
            reader.DisposeCount.Should().Be(1);
        }
        finally
        {
            if (reader.DisposeCount == 0)
            {
                reader.Dispose();
            }
        }
    }

    [TestMethod]
    public void Lookup_RepeatedName_ReadsValueEachTime()
    {
        StubStringResourceReader reader = new(ResourceTypeCode.String);
        IndexedStringResourceTable table = IndexedStringResourceTable.Create(
            reader,
            StringResourceManagerOptions.None);

        try
        {
            string? first = table.Lookup("Greeting");
            string? second = table.Lookup("Greeting");

            first.Should().Be("Hello");
            second.Should().Be("Hello");
            reader.LookupCount.Should().Be(2);
        }
        finally
        {
            table.Dispose();
        }
    }

#if !DEBUG
    [TestMethod]
    public void Lookup_AlternatingNames_DoesNotAllocate()
    {
        using IndexedStringResourceTable table = IndexedStringResourceTable.Create(
            new StubStringResourceReader(ResourceTypeCode.String),
            StringResourceManagerOptions.None);

        _ = table.Lookup("Greeting");
        _ = table.Lookup("Farewell");

        string? first;
        string? second;
        using (MemoryWatch.Create)
        {
            first = table.Lookup("Greeting");
            second = table.Lookup("Farewell");
        }

        first.Should().Be("Hello");
        second.Should().Be("Hello");
    }
#endif

    [TestMethod]
    public void Lookup_MemoryReaderRepeatedName_DecodesEachTime()
    {
        using IndexedStringResourceTable table = IndexedStringResourceTable.Create(
            new RawResourceReader(CreateResources()),
            StringResourceManagerOptions.None);

        AssertRepeatedLookup(table);
    }

    [TestMethod]
    public void Lookup_StreamReaderRepeatedName_DecodesEachTime()
    {
        using IndexedStringResourceTable table = IndexedStringResourceTable.Create(
            new StreamStringResourceReader(new MemoryStream(CreateResources(), writable: false)),
            StringResourceManagerOptions.None);

        AssertRepeatedLookup(table);
    }

    [TestMethod]
    public void Lookup_ConcurrentCustomReader_DelegatesEveryLookup()
    {
        StubStringResourceReader reader = new(ResourceTypeCode.String);
        using IndexedStringResourceTable table = IndexedStringResourceTable.Create(
            reader,
            StringResourceManagerOptions.None);

        Parallel.For(0, 8, _ =>
        {
            for (int i = 0; i < 1_000; i++)
            {
                table.Lookup("Greeting").Should().Be("Hello");
            }
        });

        reader.LookupCount.Should().Be(8_000);
    }

    [TestMethod]
    public void Lookup_MemoryReaderConcurrentNames_ReturnsRequestedValues()
    {
        using IndexedStringResourceTable table = IndexedStringResourceTable.Create(
            new RawResourceReader(CreateResources()),
            StringResourceManagerOptions.None);

        AssertConcurrentLookups(table);
    }

    [TestMethod]
    public void Lookup_StreamReaderConcurrentNames_ReturnsRequestedValues()
    {
        using IndexedStringResourceTable table = IndexedStringResourceTable.Create(
            new StreamStringResourceReader(new MemoryStream(CreateResources(), writable: false)),
            StringResourceManagerOptions.None);

        AssertConcurrentLookups(table);
    }

    [TestMethod]
    [DataRow("Greeting")]
    [DataRow("Farewell")]
    [DataRow("Missing")]
    public void Lookup_DisposedTable_ThrowsObjectDisposedException(string name)
    {
        StubStringResourceReader reader = new(ResourceTypeCode.String);
        IndexedStringResourceTable table = IndexedStringResourceTable.Create(
            reader,
            StringResourceManagerOptions.None);

        try
        {
            table.Dispose();

            Action action = () => table.Lookup(name);

            action.Should().Throw<ObjectDisposedException>();
            reader.LookupCount.Should().Be(0);
        }
        finally
        {
            table.Dispose();
        }
    }

    [TestMethod]
    public void Dispose_MultipleCalls_DisposesOwnedReaderOnce()
    {
        StubStringResourceReader reader = new(ResourceTypeCode.String);
        IndexedStringResourceTable table = IndexedStringResourceTable.Create(
            reader,
            StringResourceManagerOptions.None);

        try
        {
            table.Dispose();
            table.Dispose();

            reader.DisposeCount.Should().Be(1);
        }
        finally
        {
            table.Dispose();
        }
    }

    [TestMethod]
    public void Create_ValidationThrows_DoesNotFinalizeReaderAfterDisposal()
    {
        InvalidStringResourceReader reader = new();

        Action action = () => CreateInvalidTable(reader);

        action.Should().Throw<BadImageFormatException>();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        reader.DisposeCount.Should().Be(1);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateInvalidTable(InvalidStringResourceReader reader)
    {
        _ = IndexedStringResourceTable.Create(reader, StringResourceManagerOptions.None);
    }

    private static byte[] CreateResources()
    {
        using MemoryStream stream = new();
        using System.Resources.ResourceWriter writer = new(stream);
        writer.AddResource("Greeting", "Hello");
        writer.AddResource("Farewell", "Goodbye");
        writer.Generate();
        return stream.ToArray();
    }

    private static void AssertRepeatedLookup(IndexedStringResourceTable table)
    {
        string? first = table.Lookup("Greeting");
        string? second = table.Lookup("Greeting");

        first.Should().Be("Hello");
        second.Should().Be("Hello");
        second.Should().NotBeSameAs(first);
    }

    private static void AssertConcurrentLookups(IndexedStringResourceTable table)
    {
        Parallel.For(0, 8, worker =>
        {
            for (int i = 0; i < 1_000; i++)
            {
                bool greeting = ((i + worker) & 1) == 0;
                string name = greeting ? "Greeting" : "Farewell";
                string expected = greeting ? "Hello" : "Goodbye";
                table.Lookup(name).Should().Be(expected);
            }
        });
    }

    private sealed class InvalidStringResourceReader : IStringResourceReader
    {
        public int DisposeCount { get; private set; }

        public int ResourceCount => 1;

        public ResourceTypeCode GetResourceTypeCode(int index) =>
            throw new BadImageFormatException("Invalid resource type code.");

        public string GetResourceName(int index) => throw new NotSupportedException();

        public string GetString(int index) => throw new NotSupportedException();

        public string? Lookup(string name) =>
            throw new NotSupportedException();

        public void Dispose() => DisposeCount++;
    }
}
