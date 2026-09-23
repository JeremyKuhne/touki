// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

[TestClass]
public class ManagedAssemblyIdentityTests
{
    private static readonly Version s_version = new(1, 2, 3, 4);

    [TestMethod]
    public void ValidateSatelliteOf_MatchingIdentity_DoesNotThrow()
    {
        ManagedAssemblyIdentity owner = new("Owner", s_version, string.Empty, [1, 2, 3]);
        ManagedAssemblyIdentity satellite = new("Owner.resources", s_version, "de", [1, 2, 3]);

        Action action = () => satellite.ValidateSatelliteOf(owner, "de", contractVersion: null);

        action.Should().NotThrow();
    }

    [TestMethod]
    public void ValidateSatelliteOf_ContractVersionMatches_DoesNotThrow()
    {
        ManagedAssemblyIdentity owner = new("Owner", new(2, 0, 0, 0), string.Empty, []);
        ManagedAssemblyIdentity satellite = new("Owner.resources", s_version, "de", []);

        Action action = () => satellite.ValidateSatelliteOf(owner, "de", s_version);

        action.Should().NotThrow();
    }

    [TestMethod]
    public void ValidateSatelliteOf_VersionDoesNotMatch_ThrowsFileLoadException()
    {
        ManagedAssemblyIdentity owner = new("Owner", s_version, string.Empty, []);
        ManagedAssemblyIdentity satellite = new("Owner.resources", new(5, 0, 0, 0), "de", []);

        Action action = () => satellite.ValidateSatelliteOf(owner, "de", contractVersion: null);

        action.Should().Throw<FileLoadException>();
    }

    [TestMethod]
    public void ValidateSatelliteOf_PublicKeyDoesNotMatch_ThrowsFileLoadException()
    {
        ManagedAssemblyIdentity owner = new("Owner", s_version, string.Empty, [1, 2, 3]);
        ManagedAssemblyIdentity satellite = new("Owner.resources", s_version, "de", [3, 2, 1]);

        Action action = () => satellite.ValidateSatelliteOf(owner, "de", contractVersion: null);

        action.Should().Throw<FileLoadException>();
    }

    [TestMethod]
    public void ValidateMatches_AliasedNameAndRemainingIdentityMatch_DoesNotThrow()
    {
        ManagedAssemblyIdentity generated = new("dotnet-aot", s_version, string.Empty, [1, 2, 3]);
        ManagedAssemblyIdentity external = new("dotnet", s_version, string.Empty, [1, 2, 3]);

        Action action = () => external.ValidateMatches(generated.WithName("dotnet"));

        action.Should().NotThrow();
    }

    [TestMethod]
    public void ValidateMatches_AliasedVersionDoesNotMatch_ThrowsFileLoadException()
    {
        ManagedAssemblyIdentity generated = new("dotnet-aot", s_version, string.Empty, [1, 2, 3]);
        ManagedAssemblyIdentity external = new("dotnet", new(5, 0, 0, 0), string.Empty, [1, 2, 3]);

        Action action = () => external.ValidateMatches(generated.WithName("dotnet"));

        action.Should().Throw<FileLoadException>();
    }

    [TestMethod]
    public void ValidateMatches_AliasedPublicKeyDoesNotMatch_ThrowsFileLoadException()
    {
        ManagedAssemblyIdentity generated = new("dotnet-aot", s_version, string.Empty, [1, 2, 3]);
        ManagedAssemblyIdentity external = new("dotnet", s_version, string.Empty, [3, 2, 1]);

        Action action = () => external.ValidateMatches(generated.WithName("dotnet"));

        action.Should().Throw<FileLoadException>();
    }
}
