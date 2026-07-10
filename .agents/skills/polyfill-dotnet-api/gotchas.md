# Gotchas seen in past PRs

Detail for the [polyfill-dotnet-api](SKILL.md) skill. The
[`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) checklist enforces these;
this page explains *why*.

## 1. Empty-span pinning yields `null`

`MemoryMarshal.GetReference(default(ROS<T>))` is a null ref, so
`fixed (T* p = ...)` produces a null pointer, and net481 BCL `T*`
overloads typically throw `ArgumentNullException` instead of the
canonical "destination too short". Handle empty source / empty
destination separately before pinning. See
[EncodingExtensions.GetBytes](../../../touki/Framework/Polyfills/System.Text/EncodingExtensions.cs).

## 2. `[AggressiveInlining]` + `Unsafe.As<T, narrower>(ref param)` compares against the wrong constant on net481

When the caller passes a literal of a *signed* primitive narrower than
`int` (`sbyte`, `short`), C# `int`-promotion sign-extends it (`(sbyte)-1`
becomes `0xFFFFFFFF`). If the called method is `[AggressiveInlining]` and
reinterprets the parameter via `Unsafe.As<T, byte>(ref oldValue)` to
compare against bytes loaded with `movzx`, RyuJIT propagates the
*int-promoted* constant into the compare immediate (`cmp ecx, 0xFFFFFFFF`)
instead of the requested byte (`0xFF`). The `movzx`-loaded byte is in
`[0, 0xFF]` so the compare is *unconditionally false* - the loop runs
silently doing nothing in Release. Debug passes (no inlining). Confirmed
by disassembly in
[ReplaceUnsafeAsPerf](../../../touki.perf/ReplaceUnsafeAsPerf.cs).

**Fix:** explicitly mask in the int domain so RyuJIT must fold the high
bits to zero. The cast alone is not enough - the JIT's constant tracker
doesn't model the `conv.u1` IL op as truncating to `[0, 0xFF]` here. Use:

```csharp
byte oldByte = (byte)(Unsafe.As<T, byte>(ref oldValue) & 0xFF);
ushort oldShort = (ushort)(Unsafe.As<T, ushort>(ref oldValue) & 0xFFFF);
```

See [SpanExtensions.Replace.cs](../../../touki/Framework/Polyfills/System/SpanExtensions.Replace.cs)
for the in-place version, and the
[ReplaceUnsafeAsPerf](../../../touki.perf/ReplaceUnsafeAsPerf.cs)
benchmark for the disassembly proof. The unsigned cases (`byte`,
`ushort`, `char`) are unaffected because their int-promoted form already
has the upper bits zero. Tests on signed inputs are essential - symmetric
tests that only use `byte`/`char` will not catch this.

## 3. `Random.NextBytes(byte[])` is native on net481

The obvious managed span loop is slower per byte (loses to `byte[]` + copy
by ~1.2x on big buffers despite saving the alloc). Use a pinned
pointer loop and only on the type-exact fast path; subclasses go through
the array overload. See
[RandomExtensions](../../../touki/Framework/Polyfills/System/RandomExtensions.cs).

## 4. `HashCode` is process-local

No cross-runtime parity contract; only assert within-process determinism.

## 5. `net472` vs `net481` phrasing

Polyfill assembly TFM is `net472` (`$(DotNetFrameworkVersion)`); test TFM
is `net481` for richer GC APIs but consumes the `net472` polyfill. Don't
call the polyfill "net481-only" in commits or PR descriptions.
