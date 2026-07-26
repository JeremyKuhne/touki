# Touki Analyzers

`KlutzyNinja.Touki` ships a set of Roslyn analyzers **inside the package**. There is no
separate analyzer package to install and no configuration required to turn them on -
adding the package reference is enough, and the rules start running on the next build
and in the IDE.

The analyzers encode the conventions this library is built on: avoid hidden struct
copies, release resources deterministically, keep scratch buffers off the stack once
they get large, keep types easy to find by file name, and name a field for what it
actually is.

## Rules

| ID | Rule | Category | Default severity | Configurable | Requires |
|----|------|----------|------------------|--------------|----------|
| [TOUKI0001](#touki0001) | Use pattern matching for null checks | Usage | Warning | - | - |
| [TOUKI0002](#touki0002) | Defensive copy of a struct | Reliability | **Hidden** | - | - |
| [TOUKI0003](#touki0003) | Defensive copy of a non-copyable struct | Reliability | Warning | - | `[NonCopyable]` |
| [TOUKI0004](#touki0004) | By-value copy of a non-copyable struct | Reliability | Warning | - | `[NonCopyable]` |
| [TOUKI0010](#touki0010) | Dispose a `[MustDispose]` value deterministically | Reliability | Warning | - | `[MustDispose]` |
| [TOUKI0011](#touki0011) | Avoid large `stackalloc` allocations | Reliability | Warning | Yes | - |
| [TOUKI0020](#touki0020) | Declare one type per file | Maintainability | Warning | - | - |
| [TOUKI0021](#touki0021) | File name should match the type it declares | Maintainability | Warning | Yes | - |
| [TOUKI0030](#touki0030) | Use `ValueStringBuilder` to build strings | Performance | Warning | - | - |
| [TOUKI0040](#touki0040) | Thread-static field should carry the thread-static prefix | Naming | Warning | Yes | `[ThreadStatic]` |

Rules that list a requirement only fire on code that opts in by applying the named
attribute. The rest apply to any C# the compiler hands them.

Generated code is excluded from every rule.

---

## TOUKI0001

**Use pattern matching for null checks.** Reports `== null` and `!= null` and suggests
`is null` and `is not null`. The diagnostic is reported on the operator token, so the
squiggle lands on exactly the text to change.

```csharp
if (value != null)   // TOUKI0001
if (value is not null)
```

## TOUKI0002

**Defensive copy of a struct.** Accessing a non-`readonly` instance member through a
read-only location - an `in` parameter, a `readonly` field from outside its constructor,
a `ref readonly` local or return - silently copies the struct, runs the member against
the copy, and discards it. Any mutation the member performed is lost, and the copy costs
whatever the struct costs.

This rule ships **Hidden** on purpose. It is high volume, and on .NET Framework some hits
come from BCL structs whose members are not `readonly` there and are only avoidable by
decomposing the field. Raise it locally when you want to audit:

```ini
dotnet_diagnostic.TOUKI0002.severity = suggestion
```

## TOUKI0003

**Defensive copy of a non-copyable struct.** The same defect as TOUKI0002, but on a type
marked `[Touki.NonCopyable]`, where a copy is never acceptable. Warning by default.

## TOUKI0004

**By-value copy of a non-copyable struct.** Reports passing, returning, assigning,
declaring, or boxing a `[NonCopyable]` value by value. The message names the mechanism
and the remedy, which differ: an argument can be passed by `ref`/`in`, but a
`[NonCopyable]` field in a copyable struct is fixed by marking the containing type
instead.

Creating a value (`new`, a factory, `default`) is treated as a move rather than a copy
and is not reported.

Apply the marker to a type that owns a resource whose duplication would be a bug:

```csharp
[NonCopyable]
public ref struct BufferScope<T> { }
```

## TOUKI0010

**Dispose a `[MustDispose]` value deterministically.** Reports a value of a type marked
`[Touki.MustDispose]` that is not consumed by a `using` declaration or statement and not
disposed in a `finally`. Use it for types where finalization is not a backstop - pooled
buffers, ref structs, ref-counted handles.

```csharp
using ValueStringBuilder builder = new(stackalloc char[256]);
```

A ref struct cannot be used inside a lambda, so where the builder must be mutated through
`ref` (which a `using` variable forbids), call `ToString()` and then `Dispose()`
explicitly in a `try`/`finally`.

## TOUKI0011

**Avoid large `stackalloc` allocations.** Stack space is a fixed per-thread budget, and
overrunning it raises `StackOverflowException`, which cannot be caught and terminates the
process. Reports a `stackalloc` whose total size exceeds the configured maximum, which
defaults to **1024 bytes**.

```csharp
Span<byte> small = stackalloc byte[256];    // fine
Span<byte> large = stackalloc byte[2048];   // TOUKI0011
```

Rent from `ArrayPool<T>` instead, or use `BufferScope<T>`, which seeds from the stack and
falls back to the pool when the request outgrows the seed:

```csharp
using BufferScope<char> buffer = new(stackalloc char[256], requestedLength);
```

Configure the threshold in bytes:

```ini
dotnet_code_quality.TOUKI0011.max_stackalloc_bytes = 512
```

The rule only reports what it can size from source: a compile-time constant length with a
primitive, enum, pointer, or native-integer element type. A run-time length or a custom
struct element is left alone, because the total is not knowable at compile time. Native
integers and pointers are counted as 8 bytes.

## TOUKI0020

**Declare one type per file**, nested types included. A nested type must live in its own
file, which does not stop it from being nested - the containing type is re-declared as a
`partial` shell. A `partial` declaration that only hosts nested types is not counted as
one of the file's own types, and repeated `partial` declarations of the same type fold
into the first.

```
Value.cs                 // partial struct Value - its own members
Value.TypeFlagOfT.cs     // partial struct Value { class TypeFlag<T> }
```

There is no code fix, because Roslyn's built-in **Move type to `<Name>.cs`** refactoring
already does the job. The diagnostic is reported on the type identifier, which is exactly
where that refactoring is offered.

## TOUKI0021

**File name should match the type it declares.** The companion to TOUKI0020: once each
file holds one type, the file name should say which one.

A nested type may be named either way, and detail may follow the name after an approved
separator:

```
Foo.cs            // class Foo
Foo.Windows.cs    // class Foo, platform detail
Foo-Windows.cs    // same, different separator
Foo.Bar.cs        // class Bar nested in Foo
Bar.cs            // also fine for that nested Bar
FooWindows.cs     // TOUKI0021 - no separator, so this is a different name
```

Configure the approved separators as the set of characters to allow. The default is
`.-_`:

```ini
dotnet_code_quality.TOUKI0021.file_name_detail_separators = .-
```

Comparison is ordinal, so `foo.cs` is reported for type `Foo` even on a case-insensitive
file system. Files that declare no types - global usings, assembly-level attributes - are
not reported.

## TOUKI0030

**Use `ValueStringBuilder` to build strings.** Reports a `StringBuilder` that is only used
to build a string locally, where `Touki.Text.ValueStringBuilder` would do the same work
without the managed allocation.

```csharp
using ValueStringBuilder builder = new(stackalloc char[256]);
builder.Append(value);
return builder.ToString();
```

`ValueStringBuilder` is a `ref struct`, so the rule deliberately stays silent wherever it
could not be substituted - a builder stored in a field, returned, passed as an argument,
captured by a lambda, put in an array, assigned to a wider local, or used in an `async`
method or iterator. A warning you cannot act on is worse than no warning, so anything the
rule cannot prove is safe to convert is left alone.

## TOUKI0040

**Thread-static field should carry the thread-static prefix.** A `[ThreadStatic]` field
holds one value per thread rather than one per process, so code that reads it on a thread
that never wrote it sees a different slot. That belongs in the name, where every use site
can see it, not only at the declaration where the attribute sits.

```csharp
[ThreadStatic]
private static Value[]? t_values;   // fine

[ThreadStatic]
private static Value[]? s_values;   // TOUKI0040 - should be named 't_values'
```

The message names the field it wants, so the fix is a rename. The suggested name replaces
a leading `_`, `s_`, or thread-static prefix and camel cases what is left; an underscore
the rule does not recognize is kept, so `x_ray` becomes `t_x_ray` rather than `t_ray`.

The rule only looks at non-constant static fields. `[ThreadStatic]` on an instance field
does nothing - CA2259 reports that - and such a field is still named `_value`.

Configure the prefix:

```ini
dotnet_code_quality.TOUKI0040.thread_static_prefix = t_
```

And, if your codebase marks per-thread state with its own attribute, name it. The list is
comma-separated and each entry may be written with or without the `Attribute` suffix; the
namespace is not considered:

```ini
dotnet_code_quality.TOUKI0040.additional_thread_static_attributes = MyThreadLocal
```

### Why this is not a built-in naming rule

`.editorconfig` naming rules match a symbol group by kind, accessibility, and modifier.
`[ThreadStatic]` is an attribute, not a modifier, so no symbol group can select thread
statics - they fall into whatever rule covers ordinary statics, and IDE1006 asks for that
rule's prefix instead. The gap is tracked by
[dotnet/roslyn#32955](https://github.com/dotnet/roslyn/issues/32955), open since 2019.
TOUKI0040 fills it, and the TOUKISUPPRESS0001 suppression below keeps IDE1006 from
disagreeing.

---

## Configuring severity

Every rule responds to the standard `.editorconfig` severity keys, and can be scoped to a
directory by placing an `.editorconfig` there:

```ini
[*.cs]
dotnet_diagnostic.TOUKI0030.severity = none
```

Severities are `none`, `silent`, `suggestion`, `warning`, and `error`. If you build with
`TreatWarningsAsErrors`, note that only `warning` and above break the build - `suggestion`
keeps a rule visible in the IDE without failing CI.

This is how touki scopes rules against its own ported code. `touki/Framework/Polyfills/`
holds polyfills ported as faithfully as possible from `dotnet/runtime` so future updates
stay easy to diff, so the rules that would force an idiomatic rewrite of that code are
turned off just for that folder.

## Code fixes

`MakeMemberReadonlyCodeFixProvider` fixes TOUKI0002 and TOUKI0003 by marking the accessed
member `readonly`, which tells the compiler it does not mutate and removes the need for
the copy. It supports Fix All. The fix is only offered when the member is declared in
source; if the member really does mutate, marking it `readonly` produces a compiler error
you can act on.

## Marker attributes

Two rules are opt-in through public attributes in the `Touki` namespace:

- `[NonCopyable]` - the type owns something that must not be duplicated. Drives TOUKI0003
  and TOUKI0004.
- `[MustDispose]` - the type owns a resource that must be released on every path. Drives
  TOUKI0010.

## Suppressions

A suppression is not a rule. It removes a report another analyzer produced, and it has its
own id so it can be traced and turned off.

| ID | Suppresses | When |
|----|------------|------|
| TOUKISUPPRESS0001 | IDE1006 | A thread-static field already named the way TOUKI0040 wants |

**Ids are `TOUKISUPPRESS####`, numbered from 0001 independently of the rules.** A suppression
id is not a rule id, but the two share a namespace - an end user turns either off with the
same `dotnet_diagnostic` key or `NoWarn` entry - so the `SUPPRESS` infix keeps a suppression
out of reach of any future `TOUKI####` rule. This is the shape dotnet/runtime uses for
`SYSLIBSUPPRESS0001`. An id names the *reason*, so one id may cover several suppressed rules.

It is tracked in
[AnalyzerReleases.Unshipped.md](../touki.analyzers/AnalyzerReleases.Unshipped.md) as a
`Suppression`-category row, commented out. A suppression id is not a supported diagnostic
of any analyzer, so a live row fails the build with RS2002 and a `### Suppressions` heading
fails with RS2007. The row is maintained by hand.

**TOUKISUPPRESS0001** exists because a thread-static field matches the built-in naming rule
for ordinary statics, so without it every `t_` field draws "Missing prefix: 's_'". This is
what the hand-written `#pragma warning disable IDE1006` and `[SuppressMessage]` entries
around thread statics were for; the suppression replaces them.

Only a conforming field is suppressed. A misnamed thread static keeps its IDE1006 report
alongside TOUKI0040, so turning TOUKI0040 off cannot leave thread statics with no naming
enforcement at all.

This works even where IDE1006 is raised to `error`, because a diagnostic is suppressible
when its *default* severity is below error - which IDE1006's is - not its configured one.

Turn the suppression off to get the unsuppressed reports back:

```ini
dotnet_diagnostic.TOUKISUPPRESS0001.severity = none
```

## Release history

| Release | Rules added |
|---------|-------------|
| 0.4.0 | TOUKI0001, TOUKI0002, TOUKI0003, TOUKI0004, TOUKI0010 |
| 0.5.0 | TOUKI0020, TOUKI0030 |
| unreleased | TOUKI0011, TOUKI0021, TOUKI0040, TOUKISUPPRESS0001 |

The authoritative list lives in
[AnalyzerReleases.Shipped.md](../touki.analyzers/AnalyzerReleases.Shipped.md) and
[AnalyzerReleases.Unshipped.md](../touki.analyzers/AnalyzerReleases.Unshipped.md).
