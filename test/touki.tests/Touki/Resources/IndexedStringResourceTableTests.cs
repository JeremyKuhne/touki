// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

[TestClass]
public class IndexedStringResourceTableTests
{
    private sealed class StubStringResourceReader(ResourceTypeCode typeCode) : IStringResourceReader
    {
        public int DisposeCount { get; private set; }

        public int LookupCount { get; private set; }

        public int ResourceCount => 1;

        public ResourceTypeCode GetResourceTypeCode(int index) => typeCode;

        public string GetResourceName(int index) => "Greeting";

        public string GetString(int index) => "Hello";

        public StringResourceLookupKind Lookup(string name, out string? value)
        {
            LookupCount++;
            value = "Hello";
            return StringResourceLookupKind.Found;
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
    public void Lookup_RepeatedName_UsesCachedValue()
    {
        StubStringResourceReader reader = new(ResourceTypeCode.String);
        IndexedStringResourceTable table = IndexedStringResourceTable.Create(
            reader,
            StringResourceManagerOptions.None);
        try
        {
            table.Lookup("Greeting", out string? first).Should().Be(StringResourceLookupKind.Found);
            table.Lookup("Greeting", out string? second).Should().Be(StringResourceLookupKind.Found);

            first.Should().Be("Hello");
            second.Should().Be("Hello");
            reader.LookupCount.Should().Be(1);
        }
        finally
        {
            table.Dispose();
        }
    }

    [TestMethod]
    public async Task Dispose_LookupInProgress_WaitsBeforeDisposingBacking()
    {
        using ManualResetEventSlim lookupStarted = new();
        using ManualResetEventSlim continueLookup = new();
        using ManualResetEventSlim backingDisposed = new();
        BlockingStringResourceReader reader = new(
            lookupStarted,
            continueLookup,
            backingDisposed);
        IndexedStringResourceTable table = IndexedStringResourceTable.Create(
            reader,
            StringResourceManagerOptions.None);
        try
        {
            Task<(StringResourceLookupKind Result, string? Value)> lookup = Task.Run(() =>
            {
                StringResourceLookupKind result = table.Lookup("Greeting", out string? value);
                return (result, value);
            });

            lookupStarted.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            Task dispose = Task.Run(table.Dispose);
            SpinWait.SpinUntil(
                () => (bool)table.TestAccessor.Dynamic.Disposed,
                TimeSpan.FromSeconds(10)).Should().BeTrue();
            backingDisposed.Wait(TimeSpan.FromMilliseconds(100)).Should().BeFalse();

            continueLookup.Set();

            (StringResourceLookupKind result, string? value) = await lookup.ConfigureAwait(false);
            await dispose.ConfigureAwait(false);
            result.Should().Be(StringResourceLookupKind.Found);
            value.Should().Be("Hello");
            backingDisposed.IsSet.Should().BeTrue();
            table.Lookup("Greeting", out _).Should().Be(StringResourceLookupKind.Stale);
        }
        finally
        {
            continueLookup.Set();
            table.Dispose();
        }
    }

    [TestMethod]
    public async Task Dispose_StreamLookupInProgress_WaitsBeforeDisposingBacking()
    {
        byte[] resources;
        using (MemoryStream resourceStream = new())
        {
            using System.Resources.ResourceWriter writer = new(resourceStream);
            writer.AddResource("Greeting", "Hello");
            writer.Generate();
            resources = resourceStream.ToArray();
        }

        using ManualResetEventSlim readStarted = new();
        using ManualResetEventSlim continueRead = new();
        using ManualResetEventSlim backingDisposed = new();
        BlockingSeekableResourceStream stream = new(
            resources,
            readStarted,
            continueRead,
            backingDisposed);
        IndexedStringResourceTable table = IndexedStringResourceTable.Create(
            new StreamStringResourceReader(stream),
            StringResourceManagerOptions.None);
        try
        {
            stream.BlockReads = true;
            Task<(StringResourceLookupKind Result, string? Value)> lookup = Task.Run(() =>
            {
                StringResourceLookupKind result = table.Lookup("Greeting", out string? value);
                return (result, value);
            });

            readStarted.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            Task dispose = Task.Run(table.Dispose);
            SpinWait.SpinUntil(
                () => (bool)table.TestAccessor.Dynamic.Disposed,
                TimeSpan.FromSeconds(10)).Should().BeTrue();
            backingDisposed.Wait(TimeSpan.FromMilliseconds(100)).Should().BeFalse();

            continueRead.Set();

            (StringResourceLookupKind result, string? value) = await lookup.ConfigureAwait(false);
            await dispose.ConfigureAwait(false);
            result.Should().Be(StringResourceLookupKind.Found);
            value.Should().Be("Hello");
            backingDisposed.IsSet.Should().BeTrue();
            table.Lookup("Greeting", out _).Should().Be(StringResourceLookupKind.Stale);
        }
        finally
        {
            continueRead.Set();
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

    private sealed class InvalidStringResourceReader : IStringResourceReader
    {
        public int DisposeCount { get; private set; }

        public int ResourceCount => 1;

        public ResourceTypeCode GetResourceTypeCode(int index) =>
            throw new BadImageFormatException("Invalid resource type code.");

        public string GetResourceName(int index) => throw new NotSupportedException();

        public string GetString(int index) => throw new NotSupportedException();

        public StringResourceLookupKind Lookup(string name, out string? value) =>
            throw new NotSupportedException();

        public void Dispose() => DisposeCount++;
    }
}
