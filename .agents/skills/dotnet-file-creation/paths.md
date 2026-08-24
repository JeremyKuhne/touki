# Constructing and resolving paths

Path construction, qualification, canonicalization, and containment answer
different questions. Do not treat success at one stage as proof of another.

## Always construct with `Path.Join`

On the .NET versions covered by this skill, always use `Path.Join` to construct
a path. Never use `Path.Combine` or string concatenation.

If any argument after the first is rooted, `Path.Combine` discards every
preceding component. A caller-controlled later argument can therefore replace a
trusted root. `Path.Join` concatenates the components instead and preserves the
earlier root.

`Path.Join` is only construction. It does not normalize, validate, or establish
containment. A later component can still contain separators, `..`, volume
syntax, or a symbolic link reached during filesystem traversal.

## Test qualification, not rooting

Use `Path.IsPathFullyQualified` to ask whether a path is independent of the
current drive and current directory. `Path.IsPathRooted` asks only whether the
string contains root syntax. It is useful when a contract forbids any root, but
it does not establish that a path is stable.

Windows has rooted paths that are still relative:

| Path shape | Rooted | Fully qualified | Ambient dependency |
| --- | --- | --- | --- |
| `logs\app.txt` | No | No | Current drive and directory |
| `C:logs\app.txt` | Yes | No | Current directory for drive `C:` |
| `\logs\app.txt` | Yes | No | Current drive |
| `C:\logs\app.txt` | Yes | Yes | None |
| `\\server\share\app.txt` | Yes | Yes | None |

On Unix, a leading `/` is both rooted and fully qualified; a path without it is
neither. The distinction is therefore most visible on Windows.

Qualification is not canonicalization or validation. A fully qualified path
can still contain `.` or `..`, name a missing object, escape an intended root,
or traverse a symbolic link.

## Resolve relative paths against an explicit base

Do not call `Path.GetFullPath(path)` when `path` is not fully qualified. That
overload uses the process current directory, which any thread can change. On
Windows, drive-relative paths can also depend on per-drive current-directory
state inherited through hidden environment variables such as `=C:`.

Resolve against a known fully qualified base instead:

```csharp
if (!Path.IsPathFullyQualified(basePath))
{
    throw new ArgumentException("The base path must be fully qualified.", nameof(basePath));
}

string fullPath = Path.GetFullPath(path, basePath);
```

The two-argument overload rejects a base that is not fully qualified and makes
resolution independent of later current-directory changes. Do not derive
`basePath` from another relative value with the one-argument overload.

An explicit base makes resolution deterministic; it does not force the result
under that base. Given `N:\trusted\root` on Windows:

| Input | Result |
| --- | --- |
| `child.txt` | `N:\trusted\root\child.txt` |
| `N:child.txt` | `N:\trusted\root\child.txt` |
| `\child.txt` | `N:\child.txt` |
| `C:child.txt` | `C:\child.txt` when `C:` is a different drive |
| `C:\child.txt` | `C:\child.txt`; a fully qualified input ignores the base |

## Keep untrusted input under a root

When input must remain below a trusted root:

1. Require the root to pass `Path.IsPathFullyQualified`; canonicalize it once.
2. Define the accepted input shape. Reject root syntax when the contract is
   relative-only. For one filename, also reject directory separators, the
   volume separator, `.` and `..`.
3. Construct with `Path.Join`, never `Path.Combine`.
4. Canonicalize the joined result with
   `Path.GetFullPath(joinedPath, canonicalRoot)`.
5. Build the containment prefix by retaining an existing ending separator or
   appending `Path.DirectorySeparatorChar`. A filesystem root such as `C:\`
   already ends in one; appending another produces the wrong prefix. Verify that
   the result equals the root or starts with that prefix. Never compare only with
   the bare root: `C:\root2` shares the prefix `C:\root`. Choose comparison casing
   from known filesystem behavior; `Ordinal` is the fail-closed default.
6. Handle symbolic links and reparse points separately when physical containment
   matters. Lexical canonicalization cannot establish where links lead.

Test ordinary relative input, rooted and fully qualified input, both Windows
rooted-relative forms, `..`, alternate separators, a sibling whose name shares
the root prefix, and relevant link or reparse-point behavior.
