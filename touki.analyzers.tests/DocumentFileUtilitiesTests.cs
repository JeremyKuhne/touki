// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class DocumentFileUtilitiesTests
{
    [TestMethod]
    public void GetPathComparer_DirectorySeparator_UsesPlatformPathIdentity()
    {
        FilePathIdentity.GetPathComparer('\\').Equals("A.cs", "a.cs").Should().BeTrue();
        FilePathIdentity.GetPathComparer('/').Equals("A.cs", "a.cs").Should().BeFalse();
    }
}