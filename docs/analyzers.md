# Touki Analyzers

`KlutzyNinja.Touki.Analyzers` ships the Touki Roslyn analyzers and code fixes.
`KlutzyNinja.Touki` depends on that package, so adding the main package reference is still
enough: the rules start running on the next build and in the IDE. Reference the analyzer
package directly when you want the rules without the Touki runtime library. TOUKI0012,
TOUKI0022, TOUKI0024, and TOUKI0041 ship disabled unless a project opts in.

The analyzer package is versioned independently from `KlutzyNinja.Touki`.
Referencing Touki selects a tested minimum analyzer version; a direct analyzer
package reference can select a newer release without updating the runtime library.

The analyzers encode the conventions this library is built on: avoid hidden struct
copies, release resources deterministically, keep scratch buffers off the stack once
they get large, write formatted text without temporary strings, compose paths without
silently discarding segments, keep types easy to find by file name, keep whitespace out
of the way, and name a field for what it actually is.

## Rules

| ID | Rule | Category | Default severity | Configurable | Requires |
|----|------|----------|------------------|--------------|----------|
| [TOUKI0001](#touki0001) | Use pattern matching for null checks | Usage | Warning | - | - |
| [TOUKI0002](#touki0002) | Defensive copy of a struct | Reliability | **Hidden** | - | - |
| [TOUKI0003](#touki0003) | Defensive copy of a non-copyable struct | Reliability | Warning | - | `[NonCopyable]` |
| [TOUKI0004](#touki0004) | By-value copy of a non-copyable struct | Reliability | Warning | - | `[NonCopyable]` |
| [TOUKI0010](#touki0010) | Dispose a `[MustDispose]` value deterministically | Reliability | Warning | - | `[MustDispose]` |
| [TOUKI0011](#touki0011) | Avoid large `stackalloc` allocations | Reliability | Warning | Yes | - |
| [TOUKI0012](#touki0012) | Derive disposable classes from `DisposableBase` | Reliability | **Disabled** | - | `DisposableBase` |
| [TOUKI0020](#touki0020) | Declare one type per file | Maintainability | Warning | Yes | - |
| [TOUKI0021](#touki0021) | File name should match the type it declares | Maintainability | Warning | Yes | - |
| [TOUKI0022](#touki0022) | Avoid tab characters | Maintainability | **Disabled** | Yes | - |
| [TOUKI0023](#touki0023) | Remove trailing whitespace | Maintainability | Warning | - | - |
| [TOUKI0024](#touki0024) | Format XML documentation as nested XML | Maintainability | **Disabled** | Yes | - |
| [TOUKI0025](#touki0025) | Document types | Maintainability | Warning | Yes | - |
| [TOUKI0026](#touki0026) | Document members, parameters, and return values | Maintainability | Warning | Yes | - |
| [TOUKI0030](#touki0030) | Use `ValueStringBuilder` to build strings | Performance | Warning | - | - |
| [TOUKI0031](#touki0031) | Use `WriteFormatted` for interpolated strings | Performance | Warning | - | C# 10, `TextWriterExtensions` |
| [TOUKI0032](#touki0032) | Use `Path.Join` instead of `Path.Combine` | Reliability | Warning | - | - |
| [TOUKI0033](#touki0033) | Avoid `Path.IsPathRooted` | Reliability | Warning | - | - |
| [TOUKI0041](#touki0041) | Naming rule violation | Naming | **Disabled** | Yes | - |

Rules that require an attribute only fire on code that applies it. TOUKI0012 requires
the compilation to reference `Touki.DisposableBase`. TOUKI0031 requires C# 10 or later
and the Touki `TextWriterExtensions` handler overload. The rest apply to any C# the
compiler hands them.

Generated code is not diagnosed. TOUKI0025 counts documentation on a generated partial declaration when
analyzing the corresponding user-authored type. TOUKI0026 ignores generated member declarations.

---

## TOUKI0001

**Use pattern matching for null checks.** Reports `== null` and `!= null` and suggests
`is null` and `is not null`. The diagnostic is reported on the operator token, so the
squiggle lands on exactly the text to change.

```csharp
if (value != null)   // TOUKI0001
{
  Console.WriteLine(value);
}

if (value is not null)
{
  Console.WriteLine(value);
}
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

## TOUKI0012

**Derive disposable classes from `DisposableBase`.** Reports a class that declares
`IDisposable` in its own base list without deriving from `Touki.DisposableBase`. The base
provides thread-safe, idempotent disposal and a consistent `Dispose(bool)` override point.

```csharp
sealed class ManualResource : IDisposable           // TOUKI0012
{
  public void Dispose() { }
}

sealed class StandardResource : DisposableBase
{
  protected override void Dispose(bool disposing) { }
}
```

The rule deliberately stays silent for structs, generated code, and classes that only
inherit an `IDisposable` implementation from a base class. It also does not run when the
compilation cannot resolve `Touki.DisposableBase`.

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

The code fix moves the declaration to a new file and supports IDE solution Fix All. Nested
types remain nested inside `partial` shells that preserve the containing types' modifiers and
type parameters. Delegates are supported too. If `Type.cs` already exists, the fix tries the
qualified nested name (`Container.Type.cs`) and then an approved detail separator; it never
overwrites an existing document or file.

The fix is deliberately withheld for source files containing preprocessor directives,
file-local types, declarations that reference file-local types, and linked source files. Those
shapes cannot be moved independently without changing preprocessing or identity, editing
several projects at once, or breaking symbol binding.

File-local types are excluded from the rule entirely. The `file` modifier deliberately ties a
type and everything nested within it to that source file, so moving either declaration would
change its identity or accessibility. Other non-file-local types in the same file are still
counted normally.

To enforce top-level types now and defer nested types, set:

```ini
dotnet_code_quality.TOUKI0020.exclude_nested_types = true
```

The default is `false`.

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
not reported. An empty partial shell containing conditional directives is also omitted, so a
file for a conditionally compiled nested type does not disagree between build configurations.

The diagnostic suggests an accepted name that is unoccupied in the current compilation. The
code fix revalidates that name against the complete solution and filesystem before applying it.
Nested types prefer `Container.Type.cs`. When a partial type has declarations in several files,
the current stem is retained as detail, for example `Parser.SecurityTests.cs`, instead of
colliding on `Parser.cs`. IDE solution Fix All allocates names across the complete solution
before applying each rename. Linked source files are left unchanged because one physical rename
would have to update every project that includes the file.

### Adopting TOUKI0020 and TOUKI0021

Adopt TOUKI0020 first with IDE solution Fix All. Splitting declarations is what makes most
file names satisfy TOUKI0021, so renaming first creates throwaway work. Both severities can be
scoped by path in `.editorconfig`; keep unconverted directories at `none` and raise completed
directories to `warning` when staging a large repository.

After splitting, clean up imports through the built-in style lane, then use IDE solution Fix
All for TOUKI0021:

```powershell
dotnet format style <project> --diagnostics IDE0005 --severity warn
```

`dotnet format analyzers` cannot safely apply either structural fix. Its `MSBuildWorkspace`
persists added source documents as explicit `Compile` items that collide with SDK default
globs, and it does not support document-info renames. The providers therefore decline Fix All
and individual actions in that host instead of leaving a broken project. Rerun the IDE0005
command until it produces no changes. When moving a nested type by hand, repeat the container's
exact modifiers and type parameters on every `partial` shell; the TOUKI0020 fix does this
automatically.

For a large migration, compare the author-written metadata type names before and after in
addition to building and testing. `PEReader` and `MetadataReader.TypeDefinitions` can produce
the list; walk `GetDeclaringType()` for nested names and exclude compiler-generated names
containing `<` or `>`. This catches a missing declaration even when the remaining assembly
still builds and its tests pass.

## TOUKI0022

**Avoid tab characters.** A tab renders at whatever width the reader's editor is set to, so
a file containing tabs lines up for its author and not for anyone else.

The rule ships **disabled**. Indentation is a house style, so a project asks for it:

```ini
dotnet_diagnostic.TOUKI0022.severity = warning
```

The fix expands each tab to the **next tab stop** rather than to a fixed number of spaces,
so a tab used to align something mid-line keeps its alignment. The width comes from the
first of these that is present and parses as a positive integer:

1. `dotnet_code_quality.TOUKI0022.spaces_per_tab`
2. `tab_width` - the standard EditorConfig property
3. `indent_size` - EditorConfig's own fallback for `tab_width`
4. 4

Because these are read per file, one width can apply broadly and another to C#:

```ini
[*]
tab_width = 2

[*.cs]
tab_width = 4
```

A non-numeric value is skipped rather than treated as an error, so the legal
`indent_size = tab` falls through to the next source instead of failing the build.

## TOUKI0023

**Remove trailing whitespace.** Whitespace between the last visible character of a line and
its line break is invisible in review and becomes diff noise the next time the line is
edited. A line consisting only of whitespace is reported in full.

This is not the same as the `trim_trailing_whitespace` EditorConfig property, which is an
editor-on-save setting: it does nothing for a file written by a tool, a merge, or an editor
that does not honor it. This rule is checked on every build.

### Whitespace that is not reported

Whitespace whose exact bytes are part of the program is never reported, because deleting it
would change what the program does:

```csharp
string value = """
    trailing space here is part of the string   
    """;
```

That covers verbatim, raw, and interpolated string literals, and character literals. It
also covers text excluded by conditional compilation, which the parser never interprets -
a raw string could be sitting inside an `#if` block that is currently false.

A combining mark needs no special handling. The scan walks backwards from the end of the
line and stops at the first character that is not whitespace; a combining mark is not
whitespace, so a space that carries a following mark is never the last character on the
line and is never reported.

## TOUKI0024

**Format XML documentation as nested XML.** Enforces the repository's one-space-per-XML-level
layout for structured `///` comments while preserving documentation text and intentional prose
line breaks.

`<summary>` is always a block:

```csharp
/// <summary>
///  Gets the requested name.
/// </summary>
```

Other top-level paired elements may stay on one line when the complete physical source line fits
within the configured limit. A three-line element with exactly one content line is compacted when
it fits:

```csharp
/// <returns>The requested name.</returns>
```

An element with two or more content lines is never compacted. Nested block elements are expanded
and indented; inline elements such as `<see>`, `<paramref>`, and `<c>` remain in prose. The
contents of `<code>` and CDATA sections retain their relative indentation. Self-closing elements
remain self-closing. Malformed XML and `/** */` documentation comments are left alone.

Indentation is evaluated per contiguous logical block, such as a start tag, end tag, or run of
prose. When the first line is correctly indented, the rest of that block is left unchanged so
deliberate hanging indentation survives. When the first line is misindented, every line in the
block is shifted by the same amount.

Compaction removes only ordinary ASCII spaces and tabs that belong to the `///` layout; Unicode
whitespace such as NBSP remains documentation content. Content under effective
`xml:space="preserve"` is opaque, including inherited preservation. A nested
`xml:space="default"` element resumes normal formatting.

The rule ships **disabled** because documentation layout is a house style. Enable it with:

```ini
dotnet_diagnostic.TOUKI0024.severity = warning
```

The XML indentation step defaults to one space and can be overridden per path:

```ini
dotnet_code_quality.TOUKI0024.indent_size = 1
```

Values from 1 through 16 are accepted. Missing, invalid, non-positive, and larger values use the
default of 1.

The maximum physical line length uses the first positive integer from this list:

1. `dotnet_code_quality.TOUKI0024.max_line_length`
2. `max_line_length` - the standard EditorConfig property
3. 120

The physical length includes source indentation and the `/// ` prefix. Invalid and non-positive
values fall through to the next source rather than failing the build.

To keep analysis bounded on adversarial source, the rule stays silent for an individual comment
larger than 1 MiB, containing more than 4,096 structured XML nodes, nested more than 128 XML
elements deep, or whose formatted replacement would exceed 4 MiB.

The code fix supports document, project, solution, containing-member, and containing-type Fix All.
It groups diagnostics by document and applies the analyzer-computed replacements in one batched text
update per document, avoiding the per-diagnostic changed-solution cost of Roslyn's generic batch provider.

## TOUKI0025

**Document types.** Reports a class, struct, interface, record, enum, or delegate that does not
have one top-level `<summary>` element or a top-level `<inheritdoc>` element. Nested and
file-local types are included by default.

```csharp
class Undocumented { } // TOUKI0025

/// <summary>
///  Represents a documented type.
/// </summary>
class Documented { }
```

For a partial type, the rule counts summaries across every declaration and reports once on the
earliest user-authored declaration. Exactly one declaration may contain the summary, whether the
parts are in one file or several. A summary in generated code participates in the count and can
satisfy the rule, but a type declared only in generated code is not diagnosed. Generated files,
`generated_code = true`, `#line hidden`, `[GeneratedCode]`, and `[CompilerGenerated]`
declarations are recognized. Types lexically nested in a generated declaration are also excluded.

Only well-formed `<summary>` and `<inheritdoc>` elements in documentation blocks associated with
the declaration count. `<remarks>`, malformed XML, an unprocessed documentation block, and an
element nested inside another XML element do not substitute for top-level documentation. The
association follows the compiler: ordinary comments between the nearest documentation block and
the declaration are allowed, while an ordinary comment between documentation blocks leaves the
earlier block unprocessed. Duplicate summaries are still reported even when inheritdoc is present.

Configure the analyzed visibility with a comma-separated list:

```ini
dotnet_code_quality.TOUKI0025.api_surface = public, internal
```

Accepted values are `public`, `internal`, `private`, `file`, and `all`. The default is `all`.
Values are case-insensitive and surrounding whitespace is ignored. A missing, empty, or invalid
value falls back to `all`.

Nested types use effective visibility through their containing types. For example, a public type
nested in an internal type is `internal`. The `file` value is literal: it selects a file-local type
and non-private types nested within it, while `private` selects an explicitly private nested type.
For a partial type whose files receive different EditorConfig settings, the rule runs when any
declaring file includes the type's effective visibility.

## TOUKI0026

**Document members.** Reports a method, constructor, operator, property, indexer, field, enum
value, or event without a top-level `<summary>` or `<inheritdoc>` element. Generated declarations
are ignored. Accessors and compiler-generated backing fields are not separate documentation
targets.

```csharp
public int Count { get; } // TOUKI0026

/// <summary>
///  Gets the number of values.
/// </summary>
public int Count { get; }
```

Overrides and explicit interface implementations do not need local documentation when a
documented member exists somewhere in their base or interface hierarchy. If every member in a
source hierarchy can be inspected and none is documented, the implementation is reported. A
metadata hierarchy with unavailable XML documentation is left alone because the rule cannot prove
that documentation is absent. Implicit interface implementations remain ordinary members and must
be documented locally.

Configure the effective member visibility with:

```ini
dotnet_code_quality.TOUKI0026.api_surface = public, internal
```

Accepted values are `public`, `internal`, `private`, and `all`. The default is `public, internal`,
which means every effectively non-private member. Containing types constrain visibility: a public
member in an internal type is `internal`, and a public member in a private nested type is `private`.
Protected and protected-internal members of a public type belong to the public surface;
private-protected members belong to the internal surface. Missing, empty, or invalid values use
the default. The `file` token is not accepted because `file` applies to types, not members.

### Parameters

TOUKI0026 reports a parameter without a matching top-level `<param>` element. Methods,
constructors, operators, indexers, delegate signatures, and primary-constructor parameters
participate. Named C# 14 extension-block receivers also participate; put their `<param>` element
on the `extension(...)` block. Unnamed type-extension receivers have no source identifier and are
skipped. For a partial member, documentation on either declaration is accepted and parameters are
matched by ordinal when declaration-part names differ.

```csharp
/// <summary>Transforms a value.</summary>
public int Transform(int value) => value; // TOUKI0026
```

Disable parameter enforcement while retaining member and return enforcement with:

```ini
dotnet_code_quality.TOUKI0026.require_parameter_documentation = false
```

The default is `true`. A valid top-level `<inheritdoc>` satisfies the complete inherited contract,
including parameters.

The compiler copies XML elements from an extension block to each member declared in that block.
The analyzer follows that behavior, so a block-level summary or inheritdoc can document the
contained members. Receiver visibility is derived from those members: a private-only extension
block participates only when the `private` surface is selected.

### Return values

TOUKI0026 reports a non-void method, operator, conversion, or delegate without a top-level
`<returns>` element. Properties and indexers do not require `<value>` or `<returns>` elements;
their member documentation is still required.

```csharp
/// <summary>Gets the current count.</summary>
public int GetCount() => 0; // TOUKI0026
```

Disable return enforcement while retaining member and parameter enforcement with:

```ini
dotnet_code_quality.TOUKI0026.require_return_documentation = false
```

The default is `true`. A valid top-level `<inheritdoc>` satisfies the inherited return contract.

The member documentation rule is disabled under `touki/Framework/Polyfills` in this repository.
Those files track `dotnet/runtime`, so retaining upstream documentation coverage keeps future
updates reviewable; Touki's hand-written shared and Framework support code uses the defaults.

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

### The type needs `using Touki.Text;`

The snippet above assumes that directive is in scope. Adding it is the whole fix, and it
coexists with `using System.Text;` - the two do not collide, so no alias is needed.

Leaving it out produces different errors on the two targets, and the .NET Framework one
is misleading:

- .NET: `CS0246: The type or namespace name 'ValueStringBuilder' could not be found`,
  which points straight at the missing using.
- .NET Framework: `CS0122: 'ValueStringBuilder' is inaccessible due to its protection
  level`. `Microsoft.IO.Redist` carries an internal `System.Text.ValueStringBuilder`, and
  Touki references that package on .NET Framework, so it reaches consumers. A file that
  imports `System.Text` finds that type, and "inaccessible" reads like a broken package
  reference rather than a missing using.

Once `Touki.Text` is imported the accessible type wins and the internal one is ignored,
including when `using System.Text;` sits in an inner scope.

## TOUKI0031

**Use `WriteFormatted` for interpolated strings.** Reports a non-constant interpolated
string passed directly to the one-argument `TextWriter.Write(string)` method when the
receiver is statically typed as `TextWriter`, `StringWriter`, or `StreamWriter`, or is a
type parameter directly constrained to one of those types. `Write` first creates the complete string;
`Touki.Io.TextWriterExtensions.WriteFormatted` instead formats through
`ValueStringBuilder`. Exact `StringWriter` and `StreamWriter` instances receive the
builder content directly, without allocating that intermediate string. Custom writer
types receive a string through their virtual `Write(string)` method so overrides and
their side effects are preserved.

```csharp
writer.Write($"Rows written: {count}"); // TOUKI0031

writer.WriteFormatted($"Rows written: {count}");
```

The code fix renames the call and adds `using Touki.Io;` when needed. A named `value:`
argument is changed to the handler parameter name, `builder:`. The fix is only offered
after Roslyn proves that the changed call binds to Touki's exact handler overload, so a
competing extension or instance method cannot silently capture it. Project-level global
usings are honored. A required `using Touki.Io;` is added only when it introduces no
compiler errors or binding changes elsewhere in the document. Fix All is supported.

The rule deliberately stays silent for `WriteLine`, because a rename would lose the line
terminator; compile-time-constant interpolated strings, which create no intermediate
run-time string; string values built before the call; C# versions before 10; expression
trees, which cannot contain handler conversions; `base.Write(...)`; applicable instance
`WriteFormatted` methods; receivers statically known to be custom writer subclasses;
conditional access; and calls without an explicit receiver. Those forms cannot be
converted by the same behavior-preserving local edit or cannot use the optimized writer
path.

## TOUKI0032

**Use `Path.Join` instead of `Path.Combine`.** Reports calls bound to either
`System.IO.Path.Combine` or the downlevel `Microsoft.IO.Path.Combine` from the
strong-named `Microsoft.IO.Redist` assembly. `Combine` treats a rooted later argument as
a replacement for everything accumulated before it. That behavior is easy to miss when
a segment comes from configuration, an environment variable, or another operating
system's path syntax: a leading `/` is meaningful to both Unix and WSL tooling and may
unexpectedly discard a Windows-side prefix.

`Path.Join` never interprets a later segment as replacing the preceding path:

```csharp
string path = Path.Combine(root, segment); // TOUKI0032
string path = Path.Join(root, segment);
```

The change is intentional semantic hardening, not merely style. Code that specifically
wants rooted segments to replace earlier segments should make that branch explicit rather
than relying on `Combine` to do it implicitly.

Adopting `Join` also adopts its other argument semantics:

- null segments are treated as empty rather than rejected by overloads that validate them;
- on Windows, drive-relative forms change - for example, `Combine("C:", "child")`
  produces `C:child`, while `Join("C:", "child")` produces `C:\child`;
- the .NET Framework implementation from `Microsoft.IO.Redist` does not perform the legacy
  invalid-path-character validation that `Path.Combine` performs.

These differences are why the rule is in the Reliability category. Suppress TOUKI0032 at
an individual call only when the `Combine` behavior is deliberate and documented.

The code fix preserves the existing `Path` spelling, alias, or static import when
`System.IO.Path.Join` is available. Calls already bound to `Microsoft.IO.Path.Combine`
retain that type and are renamed to its `Join` member. On .NET Framework, where the BCL
member does not exist, calls to `System.IO.Path.Combine` are rewritten to the fully
qualified `global::Microsoft.IO.Path.Join` provided by `Microsoft.IO.Redist`. Touki
already references that package for its `net472` target. The fix is offered only when
Roslyn proves that the rewritten overload binds successfully.

Multi-targeted projects are analyzed once per target, so a shared source file can receive
the diagnostic in both its modern .NET and .NET Framework project contexts. The fix is
withheld when that physical source file appears in more than one project context: no
single replacement is portable when one target exposes `System.IO.Path.Join` and another
requires `Microsoft.IO.Path.Join`. TFM-specific files can be fixed normally. A downlevel
fix is also withheld when comments or directives appear inside the original qualified
method access, where replacing the qualifier would discard that trivia. Fix All is
supported.

## TOUKI0033

**Avoid `Path.IsPathRooted`.** Reports calls bound to either
`System.IO.Path.IsPathRooted` or the downlevel `Microsoft.IO.Path.IsPathRooted` from the
strong-named `Microsoft.IO.Redist` assembly. "Rooted" does not mean that a path resolves
independently of working-directory state.

On Windows, both drive-relative and root-relative paths are rooted but not fully
qualified:

```csharp
Path.IsPathRooted("C:child");          // true
Path.IsPathFullyQualified("C:child"); // false

Path.IsPathRooted("\\child");          // true
Path.IsPathFullyQualified("\\child"); // false
```

`C:child` resolves against the current directory recorded for drive `C:`, which can
differ from the process current directory. `\child` resolves against the current drive.
These distinctions are particularly easy to lose when paths cross Windows, Unix, and WSL
boundaries.

Use `Path.IsPathFullyQualified` when the question is whether resolving a path can be
changed by current-directory state:

```csharp
bool resolutionIsIndependent = Path.IsPathFullyQualified(path);
```

For a `net472` caller using `System.IO.Path.IsPathRooted`, the corresponding downlevel
API is `Microsoft.IO.Path.IsPathFullyQualified` from `Microsoft.IO.Redist`.

Manual replacement must account for the string-overload contracts. `IsPathRooted(null)`
returns `false`, while `IsPathFullyQualified(null)` throws `ArgumentNullException`.
Moving a `net472` BCL call to `Microsoft.IO.Redist` also adopts modern path validation:
the Redist API does not perform .NET Framework's legacy invalid-path-character check.

No code fix is offered. Some code intentionally asks whether a path has a root marker or
needs root-relative classification, and replacing that check would change its meaning.
After confirming such intent, suppress TOUKI0033 at that individual call and document the
classification being performed.

## TOUKI0041

**Naming rule violation** - a name does not follow the configured naming rules. A
replacement for the built-in IDE1006, configured with `touki_naming_*` keys instead of
`dotnet_naming_*` so the two do not read each other's configuration.

The rule ships **disabled**. Naming is a house style, so a project asks for it:

```ini
dotnet_diagnostic.TOUKI0041.severity = warning
```

With nothing else configured it enforces the same conventions the compiler assumes by
default: types, properties, methods and events are `PascalCase`, interfaces start with
`I`, and type parameters start with `T`. Fields are not covered until you say so.

### Configuration

The three key families mirror the `dotnet_naming_*` shape:

```ini
touki_naming_rule.<rule>.symbols = <group>[, <group>...]
touki_naming_rule.<rule>.style = <style>
touki_naming_rule.<rule>.severity = none | silent | suggestion | warning | error

touki_naming_symbols.<group>.applicable_kinds = namespace, class, struct, interface, enum,
    delegate, property, method, local_function, field, event, parameter, type_parameter, local
touki_naming_symbols.<group>.applicable_accessibilities = public, internal, private,
    protected, protected_internal, private_protected, local
touki_naming_symbols.<group>.required_modifiers = abstract, async, const, readonly, static,
    sealed, virtual, override, extern, volatile, required
touki_naming_symbols.<group>.excluded_modifiers = <same list>
touki_naming_symbols.<group>.required_attributes = <attribute names>
touki_naming_symbols.<group>.excluded_attributes = <attribute names>

touki_naming_style.<style>.required_prefix = _
touki_naming_style.<style>.required_suffix =
touki_naming_style.<style>.word_separator =
touki_naming_style.<style>.capitalization = pascal_case | camel_case | first_word_upper |
    all_upper | all_lower
```

An omitted `applicable_kinds`, `applicable_accessibilities` or modifier list matches
everything, as does `*`. Attribute names may be written with or without the namespace, and
the `Attribute` suffix is optional on either side - so `System.ThreadStaticAttribute`,
`ThreadStaticAttribute` and `ThreadStatic` all select the same attribute, and a class
declared as `MyThreadLocal : Attribute` is equally selected by `MyThreadLocal` or
`MyThreadLocalAttribute`.

Rules are consulted narrowest first: a group that matches on attributes beats one that
matches on modifiers, which beats one that matches only on kind. Where two rules are
equally narrow, a configured rule beats a built-in one, and beyond that the rule whose
name sorts first wins - so renaming a rule can change which of two equally narrow rules
governs a symbol.

### When a rule is ignored

An analyzer cannot report a diagnostic against an `.editorconfig`, and failing the build
over a typo in a naming preference would be worse than ignoring it. So a rule that cannot
be understood is dropped, and the built-in rules continue to apply. A rule is dropped when:

- any of `symbols`, `style` or `severity` is missing;
- `severity` is not one of the five accepted values;
- the named style has no `touki_naming_style.<style>.*` keys, or has no `capitalization`
  (the only mandatory key on a style - prefix, suffix and word separator all default to
  empty);
- a named symbol group has no `touki_naming_symbols.<group>.*` keys at all. This case
  matters: every list on such a group would be empty, and an empty list matches
  everything, so a misspelled group name would otherwise turn the rule into a catch-all
  governing every symbol in the compilation;
- any single token in `applicable_kinds`, `applicable_accessibilities`,
  `required_modifiers` or `excluded_modifiers` is unrecognized. The whole rule is dropped,
  not just the token, so a stray `record` or `operator` costs the entire rule.

A `*` in `applicable_kinds` or `applicable_accessibilities` means "everything" and stops
parsing that list, so any tokens beside it are neither honored nor validated.

### What it fixes

Each of these is a long-standing gap in IDE1006 that the touki codebase ran into.

**Attributes can select a symbol group**
([dotnet/roslyn#32955](https://github.com/dotnet/roslyn/issues/32955)). A symbol group can
only match on kind, accessibility and modifier, and `[ThreadStatic]` is an attribute. So a
`t_` prefix for thread statics is inexpressible, and thread statics fall into whatever rule
covers ordinary statics and get told to use `s_`:

```ini
touki_naming_symbols.thread_static_fields.applicable_kinds = field
touki_naming_symbols.thread_static_fields.required_modifiers = static
touki_naming_symbols.thread_static_fields.required_attributes = System.ThreadStaticAttribute

touki_naming_style.thread_static_prefix.required_prefix = t_
touki_naming_style.thread_static_prefix.capitalization = camel_case
```

**Built-in conventions survive a custom rule**
([dotnet/roslyn#71414](https://github.com/dotnet/roslyn/issues/71414)). Defining a single
`dotnet_naming_rule` makes IDE1006 drop *all* of its defaults, so a project that only wanted
to add a field convention silently stops checking types, methods, properties, events and
interfaces. Here the defaults are always appended after the configured rules. Override one
by configuring an equally or more specific rule, including one with `severity = none`.

**`const` is not `static`**
([dotnet/roslyn#23884](https://github.com/dotnet/roslyn/issues/23884),
[#15428](https://github.com/dotnet/roslyn/issues/15428),
[#23391](https://github.com/dotnet/roslyn/issues/23391)). A const field reports `IsStatic`
as true because const implies static in the language, so `required_modifiers = static`
silently demands `s_` on constants. Here `const` is only matched by `const`.

**Modifiers can be excluded, not only required**
([dotnet/roslyn#18354](https://github.com/dotnet/roslyn/issues/18354)). `excluded_modifiers`
expresses "instance fields" as `excluded_modifiers = static` rather than needing a second
rule to shadow the first.

**More modifiers** ([dotnet/roslyn#13250](https://github.com/dotnet/roslyn/issues/13250),
closed as not planned). `sealed`, `virtual`, `override`, `extern`, `volatile` and `required`
join the five upstream supports.

**`pascal_case` checks the whole name**
([dotnet/roslyn#70709](https://github.com/dotnet/roslyn/issues/70709)). Upstream treats a
name with no configured word separator as a single word, so `pascal_case` only validates
the first character and `Do_Work` passes. A style that did not ask for a word separator now
rejects an embedded underscore.

**A leading `s_` is not invented as an error**
([dotnet/roslyn#57706](https://github.com/dotnet/roslyn/issues/57706),
[#55845](https://github.com/dotnet/roslyn/issues/55845)). Upstream strips the well known
`m_`, `s_`, `t_` and `_` prefixes case-insensitively and fails when it finds one, even for a
style that requires no prefix - so `S_MAX` fails an `all_upper` rule. That check now only
runs when a prefix was actually required.

**One rule can name several symbol groups**
([dotnet/roslyn#20891](https://github.com/dotnet/roslyn/issues/20891)). This is how you get
"either attribute" matching, which a single `required_attributes` list cannot express:

```ini
touki_naming_rule.native_methods.symbols = library_import_methods, dll_import_methods
```

### Severity

`dotnet_diagnostic.TOUKI0041.severity` both enables the rule and, once set, governs the
severity of every report. A rule's own `severity` still decides whether the group is checked
at all: `severity = none` opts a group out entirely, which is how a project exempts symbols
that must keep a name it did not choose.

Prefer a rule when the symbols have something to match on. touki exempts its P/Invoke
declarations that way, keyed on `[LibraryImport]` and `[DllImport]`. Where there is nothing
to match on - a bare `const` transcribed from a C header, say - waive it one symbol at a
time instead, so the exemption names the symbol rather than a whole file:

```csharp
[assembly: SuppressMessage(
    "Naming",
    "TOUKI0041:Naming rule violation",
    Justification = "Mirrors RTLD_LAZY from <dlfcn.h>.",
    Scope = "member",
    Target = "~F:Touki.Io.Providers.MacClipboardProvider.RTLD_LAZY")]
```

If the target only exists under a conditional compilation symbol, guard the suppression
with the same `#if`. A target that does not resolve is reported as IDE0076.

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

`RenameToMatchNamingStyleCodeFixProvider` fixes TOUKI0041 by renaming the symbol to the
name the analyzer suggests, updating every reference across the solution. It does not
support Fix All, because applying several renames at once would have them fight over the
same documents.

`ReplaceTabsWithSpacesCodeFixProvider` fixes TOUKI0022, and
`RemoveTrailingWhitespaceCodeFixProvider` fixes TOUKI0023. Both edit text rather than
syntax and both support Fix All. The tab fix computes each run's spaces from the original
columns, so several fixes on one line compose without having to be applied in order.

`FormatXmlDocumentationCodeFixProvider` fixes TOUKI0024 by applying the complete replacement
computed by the analyzer from the file's options. Its document-based Fix All path applies all
non-overlapping replacements for a document together.

`UseTextWriterWriteFormattedCodeFixProvider` fixes TOUKI0031 by renaming the diagnosed
`Write` call and importing `Touki.Io` when necessary. It supports Fix All.

`UsePathJoinCodeFixProvider` fixes TOUKI0032 by selecting `System.IO.Path.Join` on modern
.NET or `Microsoft.IO.Path.Join` from `Microsoft.IO.Redist` on .NET Framework. It
preserves existing aliases and static imports where possible and supports Fix All.

## Relationship to IDE0055

The .NET SDK's `IDE0055` ([Fix formatting](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide0055))
also reports tabs and trailing whitespace when `indent_style` and `trim_trailing_whitespace`
are configured and `EnforceCodeStyleInBuild` is on. TOUKI0022 and TOUKI0023 are not novel
detection.

What they add is **granularity**. `IDE0055` is a single rule covering every formatting
option there is, so its severity is all-or-nothing: raising it to `warning` to catch tabs
also demands that every hand-aligned table and deliberate line break match the formatter's
output. Enabling it at `warning` across this repository's own test project produced 306
reports, two of which were tabs or trailing whitespace. Two narrow rules can each be set to
`warning` or `error` on their own.

## Marker attributes

Two rules are opt-in through public attributes in the `Touki` namespace:

- `[NonCopyable]` - the type owns something that must not be duplicated. Drives TOUKI0003
  and TOUKI0004.
- `[MustDispose]` - the type owns a resource that must be released on every path. Drives
  TOUKI0010.

## Release history

| Release | Rules added |
|---------|-------------|
| 0.4.0 | TOUKI0001, TOUKI0002, TOUKI0003, TOUKI0004, TOUKI0010 |
| 0.5.0 | TOUKI0020, TOUKI0030 |
| 0.6.0 | TOUKI0011, TOUKI0021, TOUKI0041 |
| 0.7.0 | TOUKI0022, TOUKI0023 |
| 0.8.0 | TOUKI0012, TOUKI0024, TOUKI0031, TOUKI0032, TOUKI0033 |
| 0.9.0 | TOUKI0025, TOUKI0026; first standalone analyzer package |
