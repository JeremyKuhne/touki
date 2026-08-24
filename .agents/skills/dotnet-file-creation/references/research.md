# Research: cross-platform file creation on .NET

Evidence base for the `dotnet-file-creation` skill. Every number below was
measured, not inferred.

Environments:

- **Windows** 11 (10.0.26200), .NET 10.0.11, via PowerShell 7.6.5 and file-based
  apps on the .NET 10 SDK.
- **Linux** Ubuntu 24.04.3 under WSL2, .NET 8.0.21, `umask 0022`, on ext4. Not on
  a `/mnt` drvfs mount, which does not carry real Unix modes and would have
  invalidated every permission measurement.

The Linux figures come from .NET 8 because that is the SDK available in that
environment. The APIs involved were introduced in .NET 7 and their behavior is
governed by `open(2)`, `mkdir(2)`, and `umask`, so they are not expected to
differ on .NET 10. Treat the Linux column as verified-on-.NET-8.

---

## 1. Where the runtime puts things

| `Environment.SpecialFolder` | Windows | Linux |
| --- | --- | --- |
| `UserProfile` | `C:\Users\<user>` | `/home/<user>` |
| `ApplicationData` | `%AppData%` | `/home/<user>/.config` |
| `LocalApplicationData` | `%LocalAppData%` | `/home/<user>/.local/share` |
| `CommonApplicationData` | `C:\ProgramData` | `/usr/share` |
| `Path.GetTempPath()` | `%TEMP%` (under the profile) | `/tmp/` |

Both per-user folders resolved under `UserProfile` on both measured systems, and
`CommonApplicationData` resolved outside it on both. The test requests
`SpecialFolderOption.Create`, because the default option returns an empty string
when the directory does not exist. Without that option, the result depends on
the state of the account image rather than only on the platform mapping.

`CommonApplicationData` is otherwise not comparable across platforms:
`C:\ProgramData` is writable by any standard user at the top level, while
`/usr/share` is root-owned. There is no portable machine-writable location.

---

## 2. Default permissions on Linux

| Object | Mode |
| --- | --- |
| `/tmp` | `777` plus the sticky bit |
| `~/.config` | `755` |
| A new subdirectory of `~/.config` via plain `Directory.CreateDirectory` | `755` |
| A file written there via `File.WriteAllText` | `644` |
| `Directory.CreateTempSubdirectory()` | `700` |
| `Path.GetTempFileName()` | `600` |
| A file created by `File.OpenHandle` | `644` |

The composite result was measured directly in this environment: a settings file
written into `~/.config/<app>/` is **reachable and readable by other local
users**, because every component grants other-execute and the file grants
other-read. The `755` parent is not a Linux invariant; desktop tooling can create
`~/.config` more restrictively, while `~/.local/share` is commonly traversable.

This is the headline difference from Windows, where the same code path lands
inside a profile directory restricted to the user, `SYSTEM`, and administrators.

`Directory.CreateTempSubdirectory` at `700` matches its documentation, which
states the parent temp directory may be shared while the created directory is
owner-only.

---

## 3. Explicit modes

### The mode must be set at creation to be safe

`File.WriteAllText` then `File.SetUnixFileMode` leaves the file at `644` in
between. `FileStreamOptions.UnixCreateMode` passes the mode to `open`, closing
the window.

### umask subtracts, never adds

Measured under `umask 022`:

| Requested | Actual |
| --- | --- |
| `600` | `600` |
| `644` | `644` |
| `666` | `644` |

A restrictive request always survives; a permissive one may be reduced. The
tests pin the invariant `(actual & ~requested) == 0` rather than an exact value,
because the exact value depends on the ambient umask.

### Directory modes reach the leaf only

`Directory.CreateDirectory(root + "/a/b/c", 700)` produced:

| Path | Mode |
| --- | --- |
| `c` (the leaf) | `700` |
| `a` (an intermediate) | `755` |

Creating each level in its own call produced `700` at every level.

**This is the opposite of the .NET Windows ACL overload**, where a security
descriptor supplied to `FileSystemAclExtensions.CreateDirectory` is applied to
every level the call creates. This is behavior of that API, not a general rule
for callers of native `CreateDirectoryW`.

---

## 4. Platform gating of the Unix APIs

Reflected attributes and observed Windows behavior:

| Member | Attribute | Windows behavior |
| --- | --- | --- |
| `File.GetUnixFileMode(string)` / `(SafeFileHandle)` | `Unsupported(windows)` | Throws `PlatformNotSupportedException` |
| `File.SetUnixFileMode(...)` | `Unsupported(windows)` | Throws `PlatformNotSupportedException` |
| `Directory.CreateDirectory(string, UnixFileMode)` | `Unsupported(windows)` | Throws `PlatformNotSupportedException` |
| `FileStreamOptions.UnixCreateMode` **setter** | `Unsupported(windows)` | Throws `PlatformNotSupportedException` |
| `FileStreamOptions.UnixCreateMode` property / getter | none | Returns `null` safely |

The attribute on `UnixCreateMode` is on the setter accessor only. Reflecting the
`PropertyInfo` custom attributes reports nothing, which initially looked like a
gap in analyzer coverage; compiling an unguarded assignment proved otherwise:

```text
error CA1416: This call site is reachable on all platforms.
'FileStreamOptions.UnixCreateMode.set' is unsupported on: 'windows'.
```

The message is *"Unix file modes are not supported on this platform."* in all
four cases.

There is no `File.CreateTempFile` in .NET 10. `Path.GetTempFileName` remains the
only built-in temp *file* API, which is why the guidance is built around
`Directory.CreateTempSubdirectory` instead.

---

## 5. Semantics measured on both platforms

| Behavior | Windows | Linux |
| --- | --- | --- |
| `FileMode.CreateNew` over an existing file | `IOException` | `IOException` |
| `FileShare.None`, second open attempt | Blocked | Blocked |
| `File.Delete` while a handle is open with `FileShare.None` | `IOException` | Succeeds, unlinked |
| `File.Move(overwrite: true)` | Replaces | Replaces |
| Path casing | Case-insensitive | Case-sensitive |
| `SetAttributes(Hidden)` on a normal file | Sets it | No throw, **no effect** |
| A dot-prefixed file reports `Hidden` | No | Yes |

The casing row is a property of the **filesystem** measured here (NTFS and ext4),
not of the operating system. macOS APFS is case-insensitive by default, so the
Unix column must not be generalized to it. The bundled test asserts casing only
on Windows and Linux for that reason, and leaves macOS unasserted.

The two `Hidden` rows carry the same caveat: they were measured on Linux only.
Whether .NET honors the macOS `UF_HIDDEN` flag was not tested, so the test scopes
those assertions to Linux as well.

`FileShare` being enforced on Linux is worth calling out, since it is often
assumed to be a Windows-only concept. .NET implements it with advisory `flock`
locks, so cooperating native processes can participate too. Processes may
ignore advisory locks, and `DOTNET_SYSTEM_IO_DISABLEFILELOCKING=1` disables this
runtime behavior entirely.

---

## 6. Path construction and qualification

Path behavior was measured separately on Windows 11 (10.0.26200), .NET 10.0.9,
via PowerShell 7.6.3:

| Input or operation | Result |
| --- | --- |
| `Path.Combine(root, rootedInput)` | Discarded `root` |
| `Path.Join(root, rootedInput)` | Preserved `root` |
| `Path.IsPathRooted("C:relative")` | `true` |
| `Path.IsPathFullyQualified("C:relative")` | `false` |
| `Path.IsPathRooted("\\root-relative")` | `true` |
| `Path.IsPathFullyQualified("\\root-relative")` | `false` |
| `Path.TrimEndingDirectorySeparator("C:\\")` | Preserved the filesystem root separator |

Changing `Environment.CurrentDirectory` between two existing directories changed
the result of `Path.GetFullPath("child.txt")`. The ordinary relative input
`child.txt` resolved under the explicit base both times when passed to
`Path.GetFullPath(path, basePath)`, and a relative `basePath` threw
`ArgumentException`.

The explicit-base overload is deterministic but is not a containment primitive.
With base `N:\trusted\root`, the measured Windows results were:

| Input | Result |
| --- | --- |
| `N:child.txt` | `N:\trusted\root\child.txt` |
| `C:child.txt` | `C:\child.txt` |
| `\child.txt` | `N:\child.txt` |
| `child.txt` | `N:\trusted\root\child.txt` |

The .NET Windows path-format documentation additionally establishes that the
one-argument overload can resolve a drive-relative path from per-drive current
directory state inherited through a hidden environment variable. That ambient
state was not manipulated in the local harness.

---

## 7. Verification status

The bundled Pester tests run on every platform and branch at run time. On this
machine they were executed on Windows: **18 passed, 3 skipped** (the three
Unix-only cases).

The Unix branches were **not** executed as PowerShell here, because installing
PowerShell into the WSL environment was out of scope. Instead every Unix
assertion was mirrored one-for-one as a C# harness and run against the .NET SDK
already present in WSL:

```text
19 assertions, all PASS
```

That verifies the behavior each Unix branch encodes. What remains unverified is
the PowerShell in those branches itself: enum comparisons, `Join-Path` with
forward slashes, and `-Skip:$IsWindows` semantics on Linux. The repository's
Linux CI Pester job executes them on every pull request, which is the intended
gate.

---

## Open questions

- Whether the Linux figures hold identically on .NET 10; the underlying syscalls
  make divergence unlikely but it was not measured.
- macOS was not measured at all. It is Unix-like and expected to match Linux for
  modes and `umask`, but `~/Library/Application Support` has different default
  permissions from `~/.config` and deserves its own measurement.
- Behavior on non-default filesystems: ReFS, network shares, and container
  overlay filesystems were not exercised.
- Service identities were not measured. Current .NET uses `GetTempPath2` where
  available, giving `SYSTEM` `%SystemRoot%\SystemTemp`; other Windows service
  identities can still inherit machine-wide `TMP` or `TEMP`. Containers can
  likewise supply a shared, missing, or read-only temp path.
