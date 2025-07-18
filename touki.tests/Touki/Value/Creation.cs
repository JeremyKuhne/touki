﻿// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.ValueTests;

public class Creation
{
    [Fact]
    public void CreateIsAllocationFree()
    {
        var watch = MemoryWatch.Create;

        Value.Create((byte)default);
        watch.Validate();
        Value.Create((sbyte)default);
        watch.Validate();
        Value.Create((char)default);
        watch.Validate();
        Value.Create((double)default);
        watch.Validate();
        Value.Create((short)default);
        watch.Validate();
        Value.Create((int)default);
        watch.Validate();
        Value.Create((long)default);
        watch.Validate();
        Value.Create((ushort)default);
        watch.Validate();
        Value.Create((uint)default);
        watch.Validate();
        Value.Create((ulong)default);
        watch.Validate();
        Value.Create((float)default);
        watch.Validate();
        Value.Create((double)default);
        watch.Validate();

        Value.Create((bool?)default);
        watch.Validate();
        Value.Create((byte?)default);
        watch.Validate();
        Value.Create((sbyte?)default);
        watch.Validate();
        Value.Create((char?)default);
        watch.Validate();
        Value.Create((double?)default);
        watch.Validate();
        Value.Create((short?)default);
        watch.Validate();
        Value.Create((int?)default);
        watch.Validate();
        Value.Create((long?)default);
        watch.Validate();
        Value.Create((ushort?)default);
        watch.Validate();
        Value.Create((uint?)default);
        watch.Validate();
        Value.Create((ulong?)default);
        watch.Validate();
        Value.Create((float?)default);
        watch.Validate();
        Value.Create((double?)default);
        watch.Validate();
    }
}
