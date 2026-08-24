---
compatibility: Targets .NET 7 or later for the Unix mode APIs; guidance assumes .NET 10 or later. The bundled tests run on Windows, Linux, and macOS under PowerShell 7 and Pester 5.7 or later.
description: Create files, directories, and paths correctly on .NET 10+ across Windows, Linux, and macOS. Use when choosing where to write temporary, persisted per-user, or machine-wide shared state; constructing paths with Path.Join or Path.Combine; distinguishing rooted from fully qualified paths; resolving paths with Path.GetFullPath; making a file readable only by its owner; calling File.Open/File.Create/FileStreamOptions/Directory.CreateDirectory/CreateTempSubdirectory/GetTempFileName; handling UnixFileMode, umask, or PlatformNotSupportedException from UnixCreateMode; responding to a CA1416 warning on a file API; or investigating behavior that differs between Windows and Linux, such as deleting an open file, FileShare, path casing, or hidden files. Also use for "where should this temp file go", "make this file user-only", "why does this work on Windows but not Linux", and atomic write-then-rename publishing.
license: MIT
metadata:
    applicability: dotnet
    binding: optional-overlay
    github-path: skills/dotnet-file-creation
    github-pinned: v0.16.1
    github-ref: refs/tags/v0.16.1
    github-repo: https://github.com/JeremyKuhne/agent-skills
    github-tree-sha: 2027ef41a5a40dd3518f053003f0fd0d6c22866c
    maturity: canary
    portability: portable
    related: windows-acls, security-review
    requires: none
    risk: local-write
name: dotnet-file-creation
---
# Creating files on .NET across platforms

If `overlay.md` exists beside this file, read it before acting; it contains
repository-specific bindings. This core remains usable without it.

Two assumptions cause most cross-platform file bugs:

1. **"A per-user directory is private."** True for a normal Windows profile.
   **Not guaranteed on Unix**, where parent modes vary and ordinary app-created
   directories and files are commonly `755` and `644`.
2. **"The temp directory belongs to me."** Usually true for an interactive
   Windows user. It is false on Unix, where `/tmp` is shared, and is not safe to
   assume for Windows services, whose temp path depends on the service identity
   and environment.

## Pick the category first

| Need | Use | Private by default |
| --- | --- | --- |
| Scratch for one operation | `Directory.CreateTempSubdirectory()` | `700` on Unix; on Windows it inherits the temp root's ACL |
| A single throwaway file | A file inside that temp subdirectory | Yes, inherited from the directory |
| Persisted per-user settings that roam | `SpecialFolder.ApplicationData` | Windows only; set the mode yourself on Unix |
| Persisted per-user caches and logs | `SpecialFolder.LocalApplicationData` | Windows only; set the mode yourself on Unix |
| Machine-wide shared state | No portable location exists. See [shared-files.md](shared-files.md) | No |

Detail per category: [temporary-files.md](temporary-files.md),
[persisted-files.md](persisted-files.md), [shared-files.md](shared-files.md).

## Construct and resolve paths deliberately

- Construct paths with `Path.Join`, never `Path.Combine` or string
  concatenation. A rooted later segment replaces every earlier segment in
  `Path.Combine`; `Path.Join` preserves them.
- Use `Path.IsPathFullyQualified` when asking whether current-directory state
  can change what a path means. `Path.IsPathRooted` is not that test: on Windows,
  `C:logs` and `\logs` are rooted but still relative.
- Resolve a path that is not fully qualified with
  `Path.GetFullPath(path, basePath)`, where `basePath` is a known fully qualified
  root. The one-argument overload uses ambient process state.
- Keep resolution separate from containment. `Path.Join`, qualification, and
  canonicalization do not reject `..`, alternate roots, or symbolic links.

The complete decision path and Windows partial-path matrix are in
[paths.md](paths.md).

## Making a file owner-only

There is **no single cross-platform API**. The runtime exposes `UnixFileMode`
for Unix and Windows ACLs for Windows, each unsupported on the other. The
portable shape is one guarded branch:

```csharp
var options = new FileStreamOptions
{
    Mode = FileMode.CreateNew,
    Access = FileAccess.Write,
};

if (!OperatingSystem.IsWindows())
{
    // Set at creation. A chmod afterwards leaves the file briefly readable.
    options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
}

using FileStream stream = File.Open(path, options);
```

`OperatingSystem.IsWindows()` is the guard CA1416 understands. The
`UnixCreateMode` **setter** carries `[UnsupportedOSPlatform("windows")]` and
throws `PlatformNotSupportedException` there, so guard the assignment, not the
open call.

On Windows the containing user-profile directory already restricts access, so no
extra work is required. Only machine-wide locations need explicit descriptors,
and that is a separate problem. The full rules, including umask, handles, and
directories, are in [permissions.md](permissions.md).

## Write atomically, then publish

Never write a consumer-visible file in place. Write beside it, flush, then move:

```csharp
string temporary = Path.Join(directory, $".{Path.GetFileName(finalPath)}.{Guid.NewGuid():N}.tmp");
await using (FileStream stream = File.Open(temporary, options))
{
    await stream.WriteAsync(payload);
    stream.Flush(flushToDisk: true);
}

File.Move(temporary, finalPath, overwrite: true);
```

The temporary file must be in the **same directory** as the destination so the
move is a rename within one volume rather than a copy.

## Behavior that differs by platform

| Behavior | Windows | Unix |
| --- | --- | --- |
| Delete a file that is open | Throws `IOException` | Succeeds; the name is unlinked |
| `FileShare` | Enforced by the OS | Advisory `flock`; honored by cooperating processes and disableable by configuration |
| `FileAttributes.Hidden` | Settable | Linux derives it from a leading dot; macOS differs |
| Directory mode or descriptor on intermediates | The .NET ACL overload applies it to every level | The Unix mode overload applies it to the leaf only |

Path casing is **not** in that table on purpose. It is a filesystem property, not
an OS one: NTFS and macOS APFS are both case-insensitive by default while Linux
ext4 is case-sensitive, and every one of them can be configured the other way.
Probe it rather than inferring it from the platform.

`FileMode.CreateNew` is exclusive and `File.Move(overwrite: true)` replaces the
destination on both. Details and the traps in
[platform-differences.md](platform-differences.md).

## Evidence

Measured behavior and test coverage are recorded in
[references/research.md](references/research.md). Primary documentation is indexed in
[references/documentation.md](references/documentation.md).

## Related skills

When the target is a machine-wide Windows location that elevated code will
trust, the Windows ACL skill owns descriptor creation and trust validation. When
the question is whether untrusted input can reach a privileged file operation,
hand off to a security review workflow. A consuming repository wires the
concrete cross-references in its overlay.
