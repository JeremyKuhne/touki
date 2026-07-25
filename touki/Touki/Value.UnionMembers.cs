// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public readonly partial struct Value
{
    // A Value is empty precisely when _object is null (see the Type property). These members are on the
    // union pattern-matching path and only need to know whether a value is present, so they test the field
    // directly rather than resolving the full Type (which pattern-matches TypeFlag, calls GetType(), and
    // disambiguates ArraySegment).
    object? IUnionMembers.Value => _object is null ? null : As<object>();

    bool IUnionMembers.HasValue => _object is not null;

    bool IUnionMembers.TryGetValue(out bool value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out byte value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out sbyte value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out char value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out short value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out ushort value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out int value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out uint value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out long value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out ulong value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out float value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out double value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out DateTime value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out DateTimeOffset value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out string? value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out ArraySegment<byte> value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out ArraySegment<char> value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out StringSegment value) => TryGetValue(out value);
}
