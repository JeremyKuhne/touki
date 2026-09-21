// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;

namespace Touki.Resources;

public partial class SatelliteStringResourceManagerTests
{
    [TestMethod]
    public void ReleaseAllResources_LocalizedDisposeThrows_ReleasesRemainingAndOwnedNeutralResources()
    {
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromRuntimeSatellites(
            NeutralBaseName(),
            s_assembly);
        manager.GetString("Greeting", CultureInfo.InvariantCulture).Should().Be("Hello");
        StringResourceManager neutralResources = manager.TestAccessor.Dynamic._neutralResources;
        ((object?)neutralResources.TestAccessor.Dynamic._cache).Should().NotBeNull();

        InvalidOperationException disposeException = new("Localized disposal failed.");
        ReleaseTrackingStringResourceReader throwingReader = new(disposeException);
        ReleaseTrackingStringResourceReader succeedingReader = new(disposeException: null);
        Dictionary<string, LocalizedStringResourceTableCache> sourceTables = new(StringComparer.Ordinal)
        {
            ["throwing"] = new(IndexedStringResourceTable.Create(
                throwingReader,
                StringResourceManagerOptions.None)),
            ["succeeding"] = new(IndexedStringResourceTable.Create(
                succeedingReader,
                StringResourceManagerOptions.None))
        };
        manager.TestAccessor.Dynamic._sourceTables = sourceTables;

        Action action = manager.ReleaseAllResources;

        action.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(disposeException);
        throwingReader.DisposeCount.Should().Be(1);
        succeedingReader.DisposeCount.Should().Be(1);
        ((object?)neutralResources.TestAccessor.Dynamic._cache).Should().BeNull();
    }

    private sealed class ReleaseTrackingStringResourceReader(Exception? disposeException) : IStringResourceReader
    {
        public int ResourceCount => 0;

        internal int DisposeCount { get; private set; }

        public ResourceTypeCode GetResourceTypeCode(int index) => throw new NotSupportedException();

        public string GetResourceName(int index) => throw new NotSupportedException();

        public string GetString(int index) => throw new NotSupportedException();

        public StringResourceLookupKind Lookup(string name, out string? value) =>
            throw new NotSupportedException();

        public void Dispose()
        {
            DisposeCount++;
            if (disposeException is not null)
            {
                throw disposeException;
            }
        }
    }
}