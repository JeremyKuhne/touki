# Touki Analyzers

`KlutzyNinja.Touki.Analyzers` ships the Touki Roslyn analyzers and code fixes.
`KlutzyNinja.Touki` depends on that package, so adding the main package reference is still
enough: the rules start running on the next build and in the IDE. Reference the analyzer
package directly when you want the rules without the Touki runtime library. TOUKI0005,
TOUKI0012, TOUKI0022, TOUKI0024, TOUKI0027, TOUKI0028, TOUKI0029, and TOUKI0041 ship disabled
unless a project opts in.

The analyzer package is versioned independently from `KlutzyNinja.Touki`.
Referencing Touki selects a tested minimum analyzer version; a direct analyzer
package reference can select a newer release without updating the runtime library.

The analyzers encode the conventions this library is built on: avoid hidden struct
copies, release resources deterministically, keep scratch buffers off the stack once
they get large, write formatted text without temporary strings, compose paths without
silently discarding segments, keep types easy to find by file name, keep whitespace out
of the way, format statement breaks consistently, name literal arguments, and name a field for
what it actually is.

## Rules

| ID | Rule | Category | Default severity | Configurable | Requires |
|----|------|----------|------------------|--------------|----------|
| [TOUKI0001](#touki0001) | Use pattern matching for null checks | Usage | Warning | - | - |
| [TOUKI0002](#touki0002) | Defensive copy of a struct | Reliability | **Hidden** | - | - |
| [TOUKI0003](#touki0003) | Defensive copy of a non-copyable struct | Reliability | Warning | - | `[NonCopyable]` |
| [TOUKI0004](#touki0004) | By-value copy of a non-copyable struct | Reliability | Warning | - | `[NonCopyable]` |
| [TOUKI0005](#touki0005) | Avoid the null-forgiving operator | Reliability | **Disabled** | - | - |
| [TOUKI0010](#touki0010) | Dispose a `[MustDispose]` value deterministically | Reliability | Warning | - | `[MustDispose]` |
| [TOUKI0011](#touki0011) | Avoid large `stackalloc` allocations | Reliability | Warning | Yes | - |
| [TOUKI0012](#touki0012) | Derive disposable classes from `DisposableBase` | Reliability | **Disabled** | - | `DisposableBase` |
| [TOUKI0020](#touki0020) | Declare one type per file | Maintainability | Warning | Yes | - |
| [TOUKI0021](#touki0021) | File name should match the type it declares | Maintainability | Warning | Yes | - |
| [TOUKI0022](#touki0022) | Avoid tab characters | Maintainability | **Disabled** | Yes | - |
| [TOUKI0023](#touki0023) | Remove trailing whitespace | Maintainability | Warning | - | - |
| [TOUKI0024](#touki0024) | Format XML documentation | Maintainability | **Disabled** | Yes | - |
| [TOUKI0025](#touki0025) | Document types | Maintainability | Warning | Yes | - |
| [TOUKI0026](#touki0026) | Document members, parameters, and return values | Maintainability | Warning | Yes | - |
| [TOUKI0027](#touki0027) | Use configured Allman formatting | Maintainability | **Disabled** | Yes | - |
| [TOUKI0028](#touki0028) | Format statement breaks around operators | Maintainability | **Disabled** | Yes | - |
| [TOUKI0029](#touki0029) | Name literal arguments | Maintainability | **Disabled** | Yes | - |
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

## TOUKI0005

**Avoid the [null-forgiving operator](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/null-forgiving).**
Reports the postfix `!` operator that suppresses nullable warnings. The operator
has no run-time effect, so it can hide an invalid nullability assumption without
protecting against a `NullReferenceException`.

```csharp
string name = GetName()!;       // TOUKI0005
string? name = GetName();       // OK
string name = GetName()
    ?? throw new Exception();   // OK

string? GetName();
```

The rule ships **disabled** because forbidding the operator is a house style.
Enable it with:

```ini
dotnet_diagnostic.TOUKI0005.severity = error
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

The rule reports allocations whose total byte size can be determined at compile time. Run-time
lengths and element types with unknown size are left alone.

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

Structs, generated code, and classes that inherit an `IDisposable` implementation are ignored.
The rule also requires a reference to `Touki.DisposableBase`.

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

The code fix moves extra declarations to new files and supports IDE solution Fix All. Nested
types remain nested inside `partial` shells with the same modifiers and type parameters. The fix
chooses an available file name and never overwrites an existing file.

File-local types are excluded. The fix is not offered for files with preprocessor directives,
declarations tied to file-local types, or linked source files.

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

Comparison is case-sensitive, so `foo.cs` is reported for type `Foo` even on a case-insensitive
file system. Files that declare no types are ignored.

The code fix selects an available accepted name, checks it against the solution and file system,
and never overwrites an existing file. It supports IDE solution Fix All. Linked source files are
left unchanged.

### Adopting TOUKI0020 and TOUKI0021

Adopt TOUKI0020 first with IDE solution Fix All. Splitting declarations makes most file names
satisfy TOUKI0021, so renaming first creates throwaway work. Both rules can be enabled by path in
`.editorconfig` for a staged migration.

After splitting, clean up imports through the built-in style lane, then use IDE solution Fix
All for TOUKI0021:

```powershell
dotnet format style <project> --diagnostics IDE0005 --severity warn
```

Use the IDE for TOUKI0020 and TOUKI0021 fixes because they add or rename files;
`dotnet format analyzers` does not apply them. Rerun the IDE0005 cleanup until it produces no
changes.

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

The code fix removes the reported whitespace and supports Fix All.

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

## TOUKI0024

**Format XML documentation.** Enforces separation from a preceding non-whitespace physical line
and the repository's one-space-per-XML-level layout for structured, line-leading `///` comments
while preserving documentation text and intentional prose line breaks.

The first line of a documentation comment is separated from preceding code by a blank line. No
blank line is required at the start of a file, immediately after an opening brace that starts a
block, or after a preprocessor directive:

```csharp
int First => 1;

/// <value>The second value.</value>
int Second => 2;
```

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
and indented, while inline elements such as `<see>`, `<paramref>`, and `<c>` remain in prose.
Existing prose wrapping and intentional hanging indentation are preserved. `<code>`, CDATA, and
content under `xml:space="preserve"` retain their indentation. Malformed XML and `/** */`
documentation comments are not reflowed; the leading blank-line requirement still applies to
`///` comments with malformed XML.

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

The physical length includes source indentation, the `///` prefix, and its following space.
Invalid and non-positive values fall through to the next source rather than failing the build.

Fix All supports document, project, solution, containing-member, and containing-type scopes.

## TOUKI0025

**Document types.** Reports a class, struct, interface, record, enum, or delegate that does not
have one top-level `<summary>` element or a valid top-level `<inheritdoc>` element. Nested and
file-local types are included by default.

```csharp
class Undocumented { } // TOUKI0025

/// <summary>
///  Represents a documented type.
/// </summary>
class Documented { }
```

For a partial type, one summary across its declarations is enough; duplicate summaries are
reported. Documentation on a generated partial declaration can satisfy the user-authored type,
while types declared only in generated code are ignored.

The `<summary>` or `<inheritdoc>` must be a well-formed top-level element. An `<inheritdoc>` is
accepted when its target, base class, or interface ultimately provides a summary. Unresolved
references and source targets without a summary do not count. Metadata without available XML
documentation is left alone. XPath-filtered inheritdoc is not supported.

Configure the analyzed declared visibility with a comma-separated list:

```ini
dotnet_code_quality.TOUKI0025.api_surface = public, internal
```

Accepted values are `public`, `internal`, `private`, `file`, and `all`; the default is `all`.
Values are case-insensitive, and invalid values use the default. `public` includes protected and
protected-internal types, `internal` includes private-protected types, and directly file-local types
use `file`. Nested types use their own declared accessibility for this setting.

To use a different set for nested types based on their effective visibility, specify:

```ini
dotnet_code_quality.TOUKI0025.effective_api_surface = public, internal
```

For nested types, `effective_api_surface` replaces `api_surface` and accounts for the accessibility
of containing types. It accepts the same values and defaults to `all`.

## TOUKI0026

**Document members.** Reports a method, constructor, operator, property, indexer, field, enum
value, or event without a top-level `<summary>` or valid `<inheritdoc>` element. Generated
declarations are ignored. Accessors and compiler-generated backing fields are not separate
documentation targets.

```csharp
public int Count { get; } // TOUKI0026

/// <summary>
///  Gets the number of values.
/// </summary>
public int Count { get; }
```

Overrides and explicit interface implementations may inherit documentation from a documented base
or interface member. Implicit interface implementations still need local documentation unless
they declare `<inheritdoc/>`. Explicit `cref` chains are followed to a documented member;
unresolved or undocumented source targets do not count. Metadata without available XML
documentation is left alone. XPath-filtered inheritdoc is not supported.

Configure the declared member visibility with:

```ini
dotnet_code_quality.TOUKI0026.api_surface = public, internal
```

Accepted values are `public`, `internal`, `private`, and `all`; the default is `public, internal`.
Values are case-insensitive, and invalid values use the default. `public` includes protected and
protected-internal members, while `internal` includes private-protected members. Members use their
own declared accessibility for this setting. The `file` value applies only to types.

To use a different set for members declared in nested types, based on their effective visibility,
specify:

```ini
dotnet_code_quality.TOUKI0026.effective_api_surface = public, internal
```

For members in nested types, `effective_api_surface` replaces `api_surface` and accounts for the
accessibility of containing types. It accepts the same values and defaults to `public, internal`.

### Parameters

TOUKI0026 reports parameters without matching top-level `<param>` elements. This includes methods,
constructors, operators, indexers, delegates, primary constructors, and named C# 14 extension
receivers. For a partial member, documentation on either declaration is accepted.

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

Documentation on a C# 14 extension block can document its contained members.

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

## TOUKI0027

**Use configured Allman formatting.** Checks every structural C# brace pair, including
declarations, executable blocks, accessor lists, initializers, anonymous objects, `with`
initializers, switch expressions, and non-empty property patterns. Empty property patterns such as
`value is { } nonNull` perform a non-null match and are not blocks, so their braces are ignored.
Interpolated-string delimiters are content rather than structural braces and are also ignored.
Before C# 11, interpolation holes are left unchanged because those language versions do not permit
the newlines that Allman formatting may introduce inside a non-verbatim interpolated string.

A construct that spans multiple physical lines puts its opening brace on a new line, leaves no
code after that opening brace, and puts its closing brace after the construct's final content
line. A complete construct may remain on one line when the whole physical line fits within the
configured maximum:

```csharp
int Count { get; }

if (ready)
{
  Run();
}
```

The rule ships **disabled** because source layout is a house style. Enable it with:

```ini
dotnet_diagnostic.TOUKI0027.severity = warning
```

All three formatting policies default to `true` and can be changed independently:

```ini
dotnet_code_quality.TOUKI0027.require_blank_line_after_closing_brace = true
dotnet_code_quality.TOUKI0027.allow_single_line_blocks = true
dotnet_code_quality.TOUKI0027.require_blank_line_after_multiline_statement = true
```

`require_blank_line_after_closing_brace` adds a blank line after a standalone `}`. It keeps
continuation clauses (`else`, `catch`, `finally`, and `do`/`while`), sibling accessors, outer
closing braces, and the end of the file adjacent. Continuation clauses are placed on the next line
without an intervening blank.

`require_blank_line_after_multiline_statement` adds a blank line after a statement whose
terminating semicolon is on a later line than its first token. It does not add one before a closing
brace or at the end of the file, and it does not apply to fields or other member declarations.
For a switch expression ending in `};`, the semicolon is the terminator.

Both blank-line policies skip preprocessor-only and inactive lines without carrying a requirement
across `#else` or `#elif`.

`allow_single_line_blocks` permits any complete structural brace pair to stay on one line. The
maximum physical line length uses the first positive integer from this list:

1. `dotnet_code_quality.TOUKI0027.max_line_length`
2. `max_line_length` - the standard EditorConfig property
3. 120

The physical length includes indentation and any source before or after the brace pair. Setting
`allow_single_line_blocks` to `false` expands every pair regardless of length. Invalid boolean
values use the default. Invalid and non-positive lengths fall through to the next source.

New indentation follows the standard `indent_style` and `indent_size` EditorConfig properties,
with four spaces as the fallback.

Fix All supports document, project, and solution scopes. Linked files are changed only when their
project contexts agree on the result. Trivia-sensitive layouts are left unchanged when they cannot
be transformed safely.

## TOUKI0028

**Format statement breaks around operators.** When an expression is already split across
physical lines, its operator begins the continuation line. Assignment-family operators, `is`,
and `=>` instead end the preceding line, and their right-hand side or body starts on the next line.

```csharp
int sum = left +
    right; // TOUKI0028

int sum = left
    + right;

int product
    = left * right; // TOUKI0028

int product =
    left * right;

bool matches = value
    is string; // TOUKI0028

bool matches = value is
    string;

int Double(int value)
    => value * 2; // TOUKI0028

int Double(int value) =>
    value * 2;
```

The rule covers common expression operators, member and conditional access, ranges, patterns,
declarations and initializers, query `let` clauses, and expression bodies. It treats `?.` and `?[`
as single operators.

Indentation follows the configured indentation, C# operator precedence, and the expression's
existing syntactic nesting. Operators at the same precedence remain aligned, while nested
expressions and precedence changes add another level. For example:

```csharp
bool result = first
  && second
    || third
      == fourth;
```

When one operator in a same-precedence chain is already wrapped, the rule wraps and aligns the
remaining operators in that chain. It does not wrap an otherwise single-line expression or collapse
an existing multiline expression.

A direct collection expression or array initializer whose delimiters are each on their own lines
aligns its opening delimiter like a block brace and keeps its contents nested. A collection or
initializer that stays on one line uses ordinary continuation indentation.

Indentation follows the standard `indent_style`, `indent_size`, and `tab_width` EditorConfig
properties, with four spaces as the fallback.

The rule ships **disabled** because statement layout is a house style. Enable it with:

```ini
dotnet_diagnostic.TOUKI0028.severity = warning
```

The fixer keeps related multiline content together when changing an operator's placement or
indentation. Trivia-sensitive layouts are left unchanged when they cannot be transformed safely.

Fix All supports document, project, and solution scopes. Linked files are changed only when their
project contexts agree on the result.

## TOUKI0029

**Name literal arguments.** Reports a selected literal passed without its parameter name. Naming
values whose meaning is not evident at the call site makes calls easier to read and review:

```csharp
Connect(true, null, default); // TOUKI0029

Connect(useTls: true, state: null, retryCount: default);
```

The rule ships **disabled** because the desired amount of argument naming is a house style. Enable
it with:

```ini
dotnet_diagnostic.TOUKI0029.severity = warning
```

By default, the rule checks `boolean`, `null`, and `default`. Configure a comma-separated set of
literal kinds to replace that default:

```ini
dotnet_code_quality.TOUKI0029.literals = integer, floating_point, character, string, boolean, null, default
```

Values are case-insensitive. `integer`, `floating_point`, and `string` include their common C#
forms; `boolean` includes `true` and `false`; and `default` includes both `default` and
`default(T)`. An empty or invalid setting uses the default set.

Parentheses, casts, checked expressions, signs, and null-forgiving operators do not hide a literal.
Named constants, already named arguments, and expanded `params` arguments are ignored.

The code fix inserts the matching parameter name. Fix All supports document, project, and solution
scopes. Fixes are withheld where language-version, expression-tree, or linked-file constraints make
the change unsafe.

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

The snippet requires `using Touki.Text;`, which can coexist with `using System.Text;` without an
alias. Without it, .NET reports `CS0246`; .NET Framework may instead report the misleading
`CS0122` because it finds an internal `System.Text.ValueStringBuilder`. Adding the
`Touki.Text` import resolves either error.

## TOUKI0031

**Use `WriteFormatted` for interpolated strings.** Reports a non-constant interpolated string
passed directly to `TextWriter.Write(string)`, including `StringWriter`, `StreamWriter`, and
matching type parameters. `WriteFormatted` can avoid creating an intermediate string while
preserving custom writer behavior.

```csharp
writer.Write($"Rows written: {count}"); // TOUKI0031

writer.WriteFormatted($"Rows written: {count}");
```

The code fix renames the call, adds `using Touki.Io;` when needed, and updates a named `value:`
argument to `builder:`. It is offered only when the replacement binds safely. Fix All is
supported.

The rule does not report `WriteLine`, constant or prebuilt strings, C# versions before 10,
expression trees, conditional access, calls without an explicit receiver, or calls where the
same replacement cannot preserve behavior.

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

The code fix uses `System.IO.Path.Join` on modern .NET and `Microsoft.IO.Path.Join` on .NET
Framework while preserving existing aliases and static imports where possible. A shared file in
a multi-targeted project is not fixed when the targets require different replacements; TFM-specific
files can be fixed normally. Trivia-sensitive calls are left unchanged. Fix All is supported.

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

Omitted symbol filters and `*` match everything. Attribute names may include or omit the namespace
and `Attribute` suffix.

When several rules match, attribute filters are more specific than modifier filters, which are
more specific than kind alone. Configured rules take precedence over built-in rules at the same
specificity; remaining ties are resolved by rule name.

### Invalid rules

Invalid or incomplete naming rules are ignored, and the built-in conventions continue to apply.
Each rule needs `symbols`, `style`, and `severity`; each referenced group and style must be defined;
and a style must specify `capitalization`. Unknown filter values invalidate the rule. Use `*`
alone when a filter should match everything.

### Additional capabilities

Compared with IDE1006, TOUKI0041:

- keeps the built-in conventions active when custom rules are added;
- supports required and excluded attributes and modifiers;
- distinguishes `const` from `static` and recognizes additional C# modifiers;
- validates the complete name against the selected capitalization style; and
- lets one rule reference several symbol groups.

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

| Rule | Code fix | Fix All |
|------|----------|---------|
| TOUKI0002, TOUKI0003 | Mark the source member `readonly` | Yes |
| TOUKI0020 | Move declarations to separate files | IDE only |
| TOUKI0021 | Rename files to match their types | IDE only |
| TOUKI0022 | Replace tabs with spaces | Yes |
| TOUKI0023 | Remove trailing whitespace | Yes |
| TOUKI0024 | Format XML documentation | Yes |
| TOUKI0027 | Apply configured Allman formatting | Yes |
| TOUKI0028 | Format statement breaks | Yes |
| TOUKI0029 | Name literal arguments | Yes |
| TOUKI0031 | Use `WriteFormatted` and add its import | Yes |
| TOUKI0032 | Replace `Path.Combine` with the appropriate `Path.Join` | Yes |
| TOUKI0041 | Rename the symbol and its references | No |

Rules not listed here do not provide a code fix.

## Relationship to IDE0055

The .NET SDK's `IDE0055` ([Fix formatting](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide0055))
can also report tabs and trailing whitespace, but it gives every formatter preference one shared
severity. TOUKI0022 and TOUKI0023 let projects enforce those checks independently.

TOUKI0027 adds configurable blank-line and line-length policies to the standard brace settings.
TOUKI0028 makes operator placement enforceable; Roslyn's
[`dotnet_style_operator_placement_when_wrapping`](https://learn.microsoft.com/visualstudio/ide/reference/code-styles-refactoring-options#dotnet_style_operator_placement_when_wrapping)
influences formatting but does not produce a diagnostic.

## Marker attributes

Two rules are opt-in through public attributes in the `Touki` namespace:

- `[NonCopyable]` - the type owns something that must not be duplicated. Drives TOUKI0003
  and TOUKI0004.
- `[MustDispose]` - the type owns a resource that must be released on every path. Drives
  TOUKI0010.

## Release history

| Release | Rules added |
|---------|-------------|
| Unshipped | TOUKI0005 |
| 0.10.0 | TOUKI0027, TOUKI0028, TOUKI0029 |
| 0.4.0 | TOUKI0001, TOUKI0002, TOUKI0003, TOUKI0004, TOUKI0010 |
| 0.5.0 | TOUKI0020, TOUKI0030 |
| 0.6.0 | TOUKI0011, TOUKI0021, TOUKI0041 |
| 0.7.0 | TOUKI0022, TOUKI0023 |
| 0.8.0 | TOUKI0012, TOUKI0024, TOUKI0031, TOUKI0032, TOUKI0033 |
| 0.9.0 | TOUKI0025, TOUKI0026; first standalone analyzer package |
