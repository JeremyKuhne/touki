// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Resources;

namespace Touki.Resources;

[TestClass]
public class StreamStringResourceReaderTests
{
    [TestMethod]
    public void ReadMembers_ValidStringResource_ReturnExpectedValues()
    {
        byte[] resources;
        using (MemoryStream resourceStream = new())
        {
            using ResourceWriter writer = new(resourceStream);
            writer.AddResource("Greeting", "Hello");
            writer.Generate();
            resources = resourceStream.ToArray();
        }

        using StringResourceManagerTestStream stream = new(
            resources,
            maximumReadSize: 3,
            canSeek: true);
        using StreamStringResourceReader reader = new(stream);

        reader.ResourceCount.Should().Be(1);
        reader.GetResourceName(0).Should().Be("Greeting");
        reader.GetResourceTypeCode(0).Should().Be(ResourceTypeCode.String);
        reader.GetString(0).Should().Be("Hello");
        reader.Lookup("Missing", out string? value).Should().Be(StringResourceLookupKind.Missing);
        value.Should().BeNull();
    }
}
