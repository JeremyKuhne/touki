// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class FilePathIdentityTests
{
    [TestMethod]
    [DataRow(true, false, true)]
    [DataRow(false, true, true)]
    [DataRow(false, false, false)]
    public void GetPathComparer_OperatingSystem_UsesPlatformPathIdentity(
        bool isWindows,
        bool isMacOS,
        bool expectedIgnoreCase)
    {
        FilePathIdentity.GetPathComparer(isWindows, isMacOS)
            .Equals("A.cs", "a.cs")
            .Should()
            .Be(expectedIgnoreCase);
    }
}