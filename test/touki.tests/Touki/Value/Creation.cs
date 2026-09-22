// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class Creation
{
    [TestMethod]
    public void CreateIsAllocationFree()
    {
        var watch = MemoryWatch.Create;

        Value.Create(value: (byte)default);
        watch.Validate();
        Value.Create(value: (sbyte)default);
        watch.Validate();
        Value.Create(value: (char)default);
        watch.Validate();
        Value.Create(value: (double)default);
        watch.Validate();
        Value.Create(value: (short)default);
        watch.Validate();
        Value.Create(value: (int)default);
        watch.Validate();
        Value.Create(value: (long)default);
        watch.Validate();
        Value.Create(value: (ushort)default);
        watch.Validate();
        Value.Create(value: (uint)default);
        watch.Validate();
        Value.Create(value: (ulong)default);
        watch.Validate();
        Value.Create(value: (float)default);
        watch.Validate();
        Value.Create(value: (double)default);
        watch.Validate();

        Value.Create(value: (bool?)default);
        watch.Validate();
        Value.Create(value: (byte?)default);
        watch.Validate();
        Value.Create(value: (sbyte?)default);
        watch.Validate();
        Value.Create(value: (char?)default);
        watch.Validate();
        Value.Create(value: (double?)default);
        watch.Validate();
        Value.Create(value: (short?)default);
        watch.Validate();
        Value.Create(value: (int?)default);
        watch.Validate();
        Value.Create(value: (long?)default);
        watch.Validate();
        Value.Create(value: (ushort?)default);
        watch.Validate();
        Value.Create(value: (uint?)default);
        watch.Validate();
        Value.Create(value: (ulong?)default);
        watch.Validate();
        Value.Create(value: (float?)default);
        watch.Validate();
        Value.Create(value: (double?)default);
        watch.Validate();
    }
}
