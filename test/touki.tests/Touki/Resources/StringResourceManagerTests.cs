// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using System.Reflection;
using System.Resources;

namespace Touki.Resources;

[TestClass]
public class StringResourceManagerTests
{
    private static readonly Assembly s_assembly = typeof(StringResourceManagerTests).Assembly;

    private static string NeutralResourceName() => s_assembly.GetManifestResourceNames()
        .Single(name => name.EndsWith("SatelliteTestStrings.resources", StringComparison.Ordinal));

    private static string NeutralBaseName() => NeutralResourceName()[..^".resources".Length];

    private static string WriteResources(string directory, params (string Key, object? Value)[] entries)
    {
        string path = Path.Join(directory, "Strings.resources");
        using ResourceWriter writer = new(path);
        foreach ((string key, object? value) in entries)
        {
            writer.AddResource(key, value);
        }

        writer.Generate();
        return path;
    }

    private static byte[] WriteResources(params (string Key, object? Value)[] entries)
    {
        using MemoryStream stream = new();
        using (ResourceWriter writer = new(stream))
        {
            foreach ((string key, object? value) in entries)
            {
                writer.AddResource(key, value);
            }

            writer.Generate();
        }

        return stream.ToArray();
    }

    [TestMethod]
    public void Constructor_ResourcesFileDoesNotExist_DoesNotOpenFile()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "Missing.resources");

        Action action = () => _ = new StringResourceManager(path);

        action.Should().NotThrow();
    }

    [TestMethod]
    public void FromAssemblyFile_AssemblyFileDoesNotExist_DoesNotOpenFile()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "Missing.dll");

        Action action = () => _ = StringResourceManager.FromAssemblyFile(NeutralBaseName(), path);

        action.Should().NotThrow();
    }

    [TestMethod]
    public void GetString_ResourcesFileReturnsString_ReturnsString()
    {
        using TempFolder folder = new();
        string path = WriteResources(folder.TempPath, ("Greeting", "Hello"));
        StringResourceManager manager = new(path);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hello");
    }

    [TestMethod]
    public void GetString_ResourceIsMissing_ReturnsNull()
    {
        using TempFolder folder = new();
        string path = WriteResources(folder.TempPath, ("Greeting", "Hello"));
        StringResourceManager manager = new(path);

        manager.GetString("Missing").Should().BeNull();
    }

    [TestMethod]
    public void GetString_ResourceIsEmpty_ReturnsEmptyString()
    {
        using TempFolder folder = new();
        string path = WriteResources(folder.TempPath, ("Empty", string.Empty));
        StringResourceManager manager = new(path);

        manager.GetString("Empty").Should().BeEmpty();
    }

    [TestMethod]
    public void GetString_ResourceIsNull_ThrowsBadImageFormatException()
    {
        using TempFolder folder = new();
        string path = WriteResources(folder.TempPath, ("NullValue", null));
        StringResourceManager manager = new(path);

        Action action = () => manager.GetString("NullValue");

        action.Should().Throw<BadImageFormatException>();
    }

    [TestMethod]
    public void GetString_IgnoreNonStringResourcesWithNull_ThrowsBadImageFormatException()
    {
        using TempFolder folder = new();
        string path = WriteResources(folder.TempPath, ("NullValue", null));
        StringResourceManager manager = new(
            path,
            StringResourceManagerOptions.IgnoreNonStringResources);

        Action action = () => manager.GetString("NullValue");

        action.Should().Throw<BadImageFormatException>();
    }

    [TestMethod]
    public void GetString_NameAndValueExceedStackBuffers_ReturnsValue()
    {
        using TempFolder folder = new();
        string name = new('n', 300);
        string value = new('v', 700);
        string path = WriteResources(folder.TempPath, (name, value));
        StringResourceManager manager = new(path);

        manager.GetString(name).Should().Be(value);
    }

    [TestMethod]
    public void GetString_ResourcesFileContainsNonString_ThrowsNotSupportedException()
    {
        using TempFolder folder = new();
        string path = WriteResources(folder.TempPath, ("Count", 42));
        StringResourceManager manager = new(path);

        Action action = () => manager.GetString("Missing");

        action.Should().Throw<NotSupportedException>();
    }

    [TestMethod]
    public void GetString_IgnoreNonStringResources_TreatsNonStringNameAsMissing()
    {
        using TempFolder folder = new();
        string path = WriteResources(folder.TempPath, ("Greeting", "Hello"), ("Count", 42));
        StringResourceManager manager = new(
            path,
            StringResourceManagerOptions.IgnoreNonStringResources);

        manager.GetString("Greeting").Should().Be("Hello");
        manager.GetString("Count").Should().BeNull();
    }

    [TestMethod]
    public void GetString_LoadedAssembly_ReturnsString()
    {
        StringResourceManager manager = new(NeutralBaseName(), s_assembly);

        manager.GetString("Greeting", CultureInfo.InvariantCulture).Should().Be("Hello");
    }

    [TestMethod]
    public void GetString_AssemblyFile_ReturnsString()
    {
        StringResourceManager manager = StringResourceManager.FromAssemblyFile(
            NeutralBaseName(),
            s_assembly.Location);

        manager.GetString("Greeting", CultureInfo.InvariantCulture).Should().Be("Hello");
    }

    [TestMethod]
    public void GetString_AssemblyFileResourceIsMissing_ThrowsMissingManifestResourceException()
    {
        StringResourceManager manager = StringResourceManager.FromAssemblyFile(
            "Missing",
            s_assembly.Location);

        Action action = () => manager.GetString("Greeting", CultureInfo.InvariantCulture);

        action.Should().Throw<MissingManifestResourceException>();
    }

    [TestMethod]
    public void ValidateAssemblyFile_ValidOwner_DoesNotThrow()
    {
        Action action = () => StringResourceManager.ValidateAssemblyFile(
            NeutralBaseName(),
            s_assembly.Location,
            s_assembly);

        action.Should().NotThrow();
    }

    [TestMethod]
    public void ValidateAssemblyFile_FileIsMissing_ThrowsFileNotFoundException()
    {
        using TempFolder folder = new();
        string assemblyFile = Path.Join(folder.TempPath, "Missing.dll");

        Action action = () => StringResourceManager.ValidateAssemblyFile(
            NeutralBaseName(),
            assemblyFile,
            s_assembly);

        action.Should().Throw<FileNotFoundException>();
    }

    [TestMethod]
    public void ValidateAssemblyFile_AssemblyIsMalformed_ThrowsBadImageFormatException()
    {
        using TempFolder folder = new();
        string assemblyFile = Path.Join(folder.TempPath, "Malformed.dll");
        System.IO.File.WriteAllBytes(assemblyFile, [0x00, 0x01, 0x02, 0x03]);

        Action action = () => StringResourceManager.ValidateAssemblyFile(
            NeutralBaseName(),
            assemblyFile,
            s_assembly);

        action.Should().Throw<BadImageFormatException>();
    }

    [TestMethod]
    public void ValidateAssemblyFile_IdentityDoesNotMatch_ThrowsFileLoadException()
    {
        Action action = () => StringResourceManager.ValidateAssemblyFile(
            NeutralBaseName(),
            typeof(StringResourceManager).Assembly.Location,
            s_assembly);

        action.Should().Throw<FileLoadException>();
    }

    [TestMethod]
    public void ValidateAssemblyFile_AliasedSimpleNameMatches_DoesNotThrow()
    {
        string simpleName = s_assembly.GetName().Name
            ?? throw new InvalidOperationException("The test assembly does not have a simple name.");

        Action action = () => StringResourceManager.ValidateAssemblyFile(
            NeutralBaseName(),
            s_assembly.Location,
            s_assembly,
            simpleName);

        action.Should().NotThrow();
    }

    [TestMethod]
    public void ValidateAssemblyFile_AliasedSimpleNameDoesNotMatch_ThrowsFileLoadException()
    {
        Action action = () => StringResourceManager.ValidateAssemblyFile(
            NeutralBaseName(),
            s_assembly.Location,
            s_assembly,
            "WrongOwner");

        action.Should().Throw<FileLoadException>();
    }

    [TestMethod]
    public void ValidateAssemblyFile_ResourceIsMissing_ThrowsMissingManifestResourceException()
    {
        Action action = () => StringResourceManager.ValidateAssemblyFile(
            "Missing",
            s_assembly.Location,
            s_assembly);

        action.Should().Throw<MissingManifestResourceException>();
    }

    [TestMethod]
    public void GetString_WithoutCulture_ReturnsString()
    {
        StringResourceManager manager = new(NeutralBaseName(), s_assembly);

        manager.GetString("Greeting").Should().Be("Hello");
    }

    [TestMethod]
    public void BaseName_LoadedAssembly_ReturnsResourceBaseName()
    {
        string baseName = NeutralBaseName();
        StringResourceManager manager = new(baseName, s_assembly);

        manager.BaseName.Should().Be(baseName);
    }

    [TestMethod]
    public void BaseName_ResourcesFile_ReturnsFileName()
    {
        StringResourceManager manager = new(Path.Join("resources", "Strings.resources"));

        manager.BaseName.Should().Be("Strings");
    }

    [TestMethod]
    public void GetString_LoadedAssemblyResourceIsMissing_ThrowsMissingManifestResourceException()
    {
        StringResourceManager manager = new("Missing", s_assembly);

        Action action = () => manager.GetString("Greeting", CultureInfo.InvariantCulture);

        action.Should().Throw<MissingManifestResourceException>();
    }

    [TestMethod]
    public void GetString_CachedFileTable_SurvivesFileDeletion()
    {
        using TempFolder folder = new();
        string path = WriteResources(folder.TempPath, ("Greeting", "Hello"));
        StringResourceManager manager = new(path);
        manager.GetString("Greeting", CultureInfo.InvariantCulture).Should().Be("Hello");
        System.IO.File.Delete(path);

        manager.GetString("Greeting", CultureInfo.InvariantCulture).Should().Be("Hello");
    }

    [TestMethod]
    public void GetString_AbandonedFileManager_ReleasesMappingDuringFinalization()
    {
        using TempFolder folder = new();
        string path = WriteResources(folder.TempPath, ("Greeting", "First"));
        WeakReference manager = LoadAndAbandon(path);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        manager.IsAlive.Should().BeFalse();
        Action action = () => WriteResources(folder.TempPath, ("Greeting", "Second"));
        action.Should().NotThrow();
    }

    [TestMethod]
    public void ReleaseAllResources_FileChanged_ReloadsValue()
    {
        using TempFolder folder = new();
        string path = WriteResources(folder.TempPath, ("Greeting", "First"));
        StringResourceManager manager = new(path);
        manager.GetString("Greeting", CultureInfo.InvariantCulture).Should().Be("First");

        manager.ReleaseAllResources();
        WriteResources(folder.TempPath, ("Greeting", "Second"));

        manager.GetString("Greeting", CultureInfo.InvariantCulture).Should().Be("Second");
    }

    [TestMethod]
    public void ReleaseAllResources_AssemblyFile_ReopensFile()
    {
        int openCount = 0;

        MappedMemoryManager OpenFile(string path)
        {
            openCount++;
            return MappedMemoryManager.CreateFromFile(path);
        }

        StringResourceManager manager = StringResourceManager.FromAssemblyFile(
            NeutralBaseName(),
            s_assembly.Location,
            OpenFile);

        manager.GetString("Greeting").Should().Be("Hello");
        manager.GetString("Greeting").Should().Be("Hello");
        openCount.Should().Be(1);

        manager.ReleaseAllResources();

        manager.GetString("Greeting").Should().Be("Hello");
        openCount.Should().Be(2);
    }

    [TestMethod]
    public void GetString_StreamFactoryStartsAtCurrentPosition_ReturnsString()
    {
        byte[] resources = WriteResources(("Greeting", "Hello"));
        byte[] padded = new byte[resources.Length + 7];
        resources.CopyTo(padded, 7);
        StringResourceManager manager = new("Strings", () => new MemoryStream(padded) { Position = 7 });

        manager.GetString("Greeting").Should().Be("Hello");
    }

    [TestMethod]
    public unsafe void GetString_PositionedUnmanagedStream_ReturnsString()
    {
        byte[] resources = WriteResources(("Greeting", "Hello"));
        byte[] padded = new byte[resources.Length + 7];
        resources.CopyTo(padded, 7);

        fixed (byte* pointer = padded)
        {
            using UnmanagedMemoryStream stream = new(pointer, padded.Length);
            stream.Position = 7;
            StringResourceManager manager = new("Strings", () => stream);

            manager.GetString("Greeting").Should().Be("Hello");
        }
    }

    [TestMethod]
    public unsafe void GetString_EmptyUnmanagedStream_ThrowsArgumentException()
    {
        byte[] storage = new byte[1];

        fixed (byte* pointer = storage)
        {
            using UnmanagedMemoryStream stream = new(pointer, length: 0);
            StringResourceManager manager = new("Strings", () => stream);

            Action action = () => manager.GetString("Greeting");

            action.Should().Throw<ArgumentException>();
        }
    }

    [TestMethod]
    public void GetString_SeekablePartialReadStream_ReturnsStringAndRetainsStreamUntilRelease()
    {
        byte[] resources = WriteResources(("Greeting", "Hello"));
        StringResourceManagerTestStream? stream = null;
        StringResourceManager manager = new(
            "Strings",
            () => stream = new(resources, maximumReadSize: 3, canSeek: true));

        manager.GetString("Greeting").Should().Be("Hello");

        stream.Should().NotBeNull();
        stream.IsDisposed.Should().BeFalse();
        manager.ReleaseAllResources();
        stream.IsDisposed.Should().BeTrue();
    }

    [TestMethod]
    public void GetString_NonSeekableStream_ThrowsNotSupportedExceptionAndDisposesStream()
    {
        byte[] resources = WriteResources(("Greeting", "Hello"));
        StringResourceManagerTestStream? stream = null;
        StringResourceManager manager = new("Strings", () => stream = new(resources, maximumReadSize: 3));

        Action action = () => manager.GetString("Greeting");

        action.Should().Throw<NotSupportedException>();
        stream.Should().NotBeNull();
        stream.IsDisposed.Should().BeTrue();
    }

    [TestMethod]
    public void GetString_MalformedStream_DisposesOnceWithoutMaskingParseException()
    {
        StringResourceManagerTestStream? stream = null;
        StringResourceManager manager = new(
            "Strings",
            () => stream = new(
                new byte[64],
                maximumReadSize: 3,
                canSeek: true,
                throwOnSecondDispose: true));

        Action action = () => manager.GetString("Greeting");

        action.Should().Throw<ArgumentException>();
        stream.Should().NotBeNull();
        stream.DisposeCount.Should().Be(1);
    }

    [TestMethod]
    public void GetString_IgnoreNonStringResourcesWithUndefinedMemoryType_ThrowsBadImageFormatException()
    {
        byte[] resources = WriteResources(("Greeting", "Hello"));
        SetTypeCode(resources, "Greeting", typeCode: 0x11);
        StringResourceManager manager = new(
            "Strings",
            () => new MemoryStream(resources, writable: false),
            StringResourceManagerOptions.IgnoreNonStringResources);

        Action action = () => manager.GetString("Greeting");

        action.Should().Throw<BadImageFormatException>();
    }

    [TestMethod]
    public void GetString_IgnoreNonStringResourcesWithUndefinedStreamType_ThrowsBadImageFormatException()
    {
        byte[] resources = WriteResources(("Greeting", "Hello"));
        SetTypeCode(resources, "Greeting", typeCode: 0x11);
        StringResourceManager manager = new(
            "Strings",
            () => new StringResourceManagerTestStream(resources, maximumReadSize: 3, canSeek: true),
            StringResourceManagerOptions.IgnoreNonStringResources);

        Action action = () => manager.GetString("Greeting");

        action.Should().Throw<BadImageFormatException>();
    }

    [TestMethod]
    public void GetString_StreamWithOverflowingTypeCode_ThrowsBadImageFormatException()
    {
        byte[] resources = SetOverflowingTypeCode(WriteResources(("Greeting", "Hello")), "Greeting");
        StringResourceManager manager = new(
            "Strings",
            () => new StringResourceManagerTestStream(resources, maximumReadSize: 3, canSeek: true));

        Action action = () => manager.GetString("Greeting");

        action.Should().Throw<BadImageFormatException>();
    }

    [TestMethod]
    public void GetString_StreamFactoryReturnsNull_ThrowsInvalidOperationException()
    {
        StringResourceManager manager = new("Strings", StringResourceManagerNullStreamFactory.Create);

        Action action = () => manager.GetString("Greeting");

        action.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void GetString_StreamFactoryThrows_InvokesFactoryOncePerGeneration()
    {
        int invocationCount = 0;
        StringResourceManager manager = new(
            "Strings",
            () =>
            {
                invocationCount++;
                throw new System.IO.IOException("Factory failure.");
            });

        Action action = () => manager.GetString("Greeting");

        action.Should().ThrowExactly<System.IO.IOException>()
            .WithMessage("Factory failure.");

        action.Should().ThrowExactly<System.IO.IOException>()
            .WithMessage("Factory failure.");

        invocationCount.Should().Be(1);

        manager.ReleaseAllResources();

        action.Should().ThrowExactly<System.IO.IOException>()
            .WithMessage("Factory failure.");

        invocationCount.Should().Be(2);
    }

    [TestMethod]
    public void GetString_StreamIsMalformed_ThrowsAndDisposesStream()
    {
        StringResourceManagerTestStream? stream = null;
        StringResourceManager manager = new(
            "Strings",
            () => stream = new([0x00, 0x01], maximumReadSize: 1, canSeek: true));

        Action action = () => manager.GetString("Greeting");

        action.Should().Throw<BadImageFormatException>();
        stream.Should().NotBeNull();
        stream.IsDisposed.Should().BeTrue();
    }

    [TestMethod]
    public void GetString_ResourcesFileIsMalformed_ThrowsArgumentException()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "Malformed.resources");
        System.IO.File.WriteAllBytes(path, [0x00, 0x01]);
        StringResourceManager manager = new(path);

        Action action = () => manager.GetString("Greeting");

        action.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void GetString_ResourcesFileFailedThenCreated_RetriesAfterRelease()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "Strings.resources");
        StringResourceManager manager = new(path);

        Action action = () => manager.GetString("Greeting");

        action.Should().Throw<System.IO.FileNotFoundException>();
        WriteResources(folder.TempPath, ("Greeting", "Hello"));
        action.Should().Throw<System.IO.FileNotFoundException>();

        manager.ReleaseAllResources();

        manager.GetString("Greeting").Should().Be("Hello");
    }

    [TestMethod]
    public async Task GetString_ConcurrentFirstLookup_InvokesFactoryOnce()
    {
        byte[] resources = WriteResources(("Greeting", "Hello"));
        using ManualResetEventSlim loadStarted = new();
        using ManualResetEventSlim continueLoad = new();
        int invocationCount = 0;
        StringResourceManager manager = new("Strings", CreateStream);

        Stream CreateStream()
        {
            Interlocked.Increment(ref invocationCount);
            loadStarted.Set();
            continueLoad.Wait();
            return new MemoryStream(resources);
        }

        Task<string?> first = Task.Run(() => manager.GetString("Greeting"));
        loadStarted.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        Task<string?> second = Task.Run(() => manager.GetString("Greeting"));
        continueLoad.Set();

        string?[] values = await Task.WhenAll(first, second).ConfigureAwait(continueOnCapturedContext: false);
        values.Should().Equal("Hello", "Hello");
        invocationCount.Should().Be(1);
    }

    [TestMethod]
    public async Task ReleaseAllResources_LoadInProgress_DoesNotRetainOldGeneration()
    {
        byte[] firstResources = WriteResources(("Greeting", "First"));
        byte[] secondResources = WriteResources(("Greeting", "Second"));
        using ManualResetEventSlim loadStarted = new();
        using ManualResetEventSlim continueLoad = new();
        using ManualResetEventSlim releaseWaiting = new();
        int invocationCount = 0;
        StringResourceManager manager = new("Strings", CreateStream);

        Stream CreateStream()
        {
            int invocation = Interlocked.Increment(ref invocationCount);
            if (invocation == 1)
            {
                loadStarted.Set();
                continueLoad.Wait();
                return new MemoryStream(firstResources);
            }

            return new MemoryStream(secondResources);
        }

        Task<string?> lookup = Task.Run(() => manager.GetString("Greeting"));
        loadStarted.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        Task release = Task.Run(() => manager.ReleaseAllResources(releaseWaiting.Set));
        releaseWaiting.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        continueLoad.Set();

        (await lookup.ConfigureAwait(continueOnCapturedContext: false)).Should().BeOneOf("First", "Second");
        await release.ConfigureAwait(continueOnCapturedContext: false);
        manager.GetString("Greeting").Should().Be("Second");
        invocationCount.Should().Be(2);
    }

    [TestMethod]
    public void IgnoreCase_Get_ReturnsFalse()
    {
        StringResourceManager manager = new(NeutralBaseName(), s_assembly);

        manager.IgnoreCase.Should().BeFalse();
    }

    [TestMethod]
    public void IgnoreCase_Set_ThrowsNotSupportedException()
    {
        StringResourceManager manager = new(NeutralBaseName(), s_assembly);

        Action action = () => manager.IgnoreCase = false;

        action.Should().Throw<NotSupportedException>();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference LoadAndAbandon(string path)
    {
        StringResourceManager manager = new(path);
        manager.GetString("Greeting").Should().Be("First");
        return new(manager);
    }

    private static void SetTypeCode(byte[] resources, string name, byte typeCode)
    {
        using RawResourceReader reader = new(resources);
        reader.TryFindResource(name, out ResourceLocation location).Should().BeTrue();
        location.TypeCode.Should().Be(ResourceTypeCode.String);
        resources[location.ContentOffset - 2] = typeCode;
    }

    private static byte[] SetOverflowingTypeCode(byte[] resources, string name)
    {
        int typeCodeOffset;
        using (RawResourceReader reader = new(resources))
        {
            reader.TryFindResource(name, out ResourceLocation location).Should().BeTrue();
            location.TypeCode.Should().Be(ResourceTypeCode.String);
            typeCodeOffset = location.ContentOffset - 2;
        }

        byte[] malformed = new byte[resources.Length + 4];
        resources.AsSpan(0, typeCodeOffset).CopyTo(malformed);
        malformed[typeCodeOffset] = 0x81;
        malformed[typeCodeOffset + 1] = 0x80;
        malformed[typeCodeOffset + 2] = 0x80;
        malformed[typeCodeOffset + 3] = 0x80;
        malformed[typeCodeOffset + 4] = 0x10;
        resources.AsSpan(typeCodeOffset + 1).CopyTo(malformed.AsSpan(typeCodeOffset + 5));
        return malformed;
    }
}