# Permissions across platforms

## What the runtime actually exposes

There is no cross-platform permission API. The runtime gives you two
platform-specific ones and expects you to branch.

| API | Platform | Attribute |
| --- | --- | --- |
| `FileStreamOptions.UnixCreateMode` (setter) | Unix | `[UnsupportedOSPlatform("windows")]` |
| `File.GetUnixFileMode` / `SetUnixFileMode` | Unix | `[UnsupportedOSPlatform("windows")]` |
| `Directory.CreateDirectory(path, UnixFileMode)` | Unix | `[UnsupportedOSPlatform("windows")]` |
| `FileSystemAclExtensions`, `DirectorySecurity`, `FileSecurity` | Windows | `[SupportedOSPlatform("windows")]` |

All four Unix members throw `PlatformNotSupportedException` on Windows with
*"Unix file modes are not supported on this platform."* CA1416 flags an
unguarded call at build time, so keep that analyzer on.

Note where the attribute sits on `UnixCreateMode`: on the **setter**, not the
property. Reflecting over the `PropertyInfo` shows nothing, and reading the
property is always safe. Only the assignment is platform-gated.

## The portable shape

```csharp
var options = new FileStreamOptions
{
    Mode = FileMode.CreateNew,
    Access = FileAccess.Write,
};

if (!OperatingSystem.IsWindows())
{
    options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
}

using FileStream stream = File.Open(path, options);
```

`OperatingSystem.IsWindows()` is the guard the analyzer recognizes. A
`RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` check is equivalent at run
time but does not silence CA1416.

On Windows nothing more is needed for per-user storage, because the profile
directory already restricts access. Only machine-wide locations need an explicit
descriptor there.

## Set the mode at creation

`File.WriteAllText` followed by `File.SetUnixFileMode` leaves the file
world-readable for the interval between the two calls. Under a default `022`
umask a plain create is `644`, so that window is real.

Setting the mode through `FileStreamOptions` closes it, because the mode is
passed to the underlying `open` call.

The same principle appears on Windows in the ACL skill: supply the security
descriptor at creation rather than creating and then repairing.

## umask can only take bits away

Measured under `umask 022`:

| Requested | Actual |
| --- | --- |
| `600` | `600` |
| `644` | `644` |
| `666` | `644` |

So a restrictive request always survives, and a permissive one may be silently
reduced. Two consequences:

- Owner-only (`600`, `700`) is safe to depend on.
- Never rely on a mode being *granted*. If a group must read the file, verify the
  result rather than assuming the request took effect, or set the mode
  explicitly afterwards with `File.SetUnixFileMode`.

## Directories: the mode reaches the leaf only

`Directory.CreateDirectory(path, mode)` applies the mode to the directory it
creates at the end of the path. Intermediates it has to create along the way get
the process default instead.

```csharp
Directory.CreateDirectory("/home/u/app/a/b/c", OwnerOnly);
// c   -> 700
// a,b -> 755 under a 022 umask
```

This is the **opposite** of the .NET Windows ACL overload, which reuses a
supplied security descriptor for every level it creates. Create each level you
care about in its own call:

```csharp
string current = root;
foreach (string segment in segments)
{
    current = Path.Join(current, segment);
    Directory.CreateDirectory(current, OwnerOnlyDirectory);
}
```

Note also that `CreateDirectory` on an **existing** directory does not change its
mode. Tightening an existing tree is a separate, deliberate migration.

## Handles

`File.OpenHandle` has no `UnixCreateMode` overload, so a file it creates gets the
default (`644` under a `022` umask). If you need the handle API and owner-only
permissions, set the mode on the handle immediately:

```csharp
using SafeFileHandle handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.Write);

if (!OperatingSystem.IsWindows())
{
    File.SetUnixFileMode(handle, UnixFileMode.UserRead | UnixFileMode.UserWrite);
}
```

Setting on the handle rather than the path removes the path race, but a brief
window with default permissions remains. When that matters, use
`File.Open(path, options)` with `UnixCreateMode` and accept the `FileStream`.

## Directory permissions do most of the work

A `700` directory makes its contents unreachable by other users regardless of the
files' own modes, because traversal requires execute permission on every
component. That is usually the better lever:

- One call protects everything you put inside.
- It survives files created later by code that forgot to set a mode.

Set restrictive modes on individual files as defense in depth for sensitive
content, not as the primary mechanism.

## What the runtime does not expose

- **File ownership.** There is no BCL API for the owning UID or GID on Unix. If a
  trust decision depends on ownership you need a native call or `stat`.
- **ACLs on Unix.** POSIX ACLs and extended attributes are not surfaced.
- **Unix modes on Windows.** There is no translation layer; the concepts do not
  map, and the runtime does not pretend otherwise.

If you find yourself wanting a single call that means "user only" on every
platform, write that helper yourself with one guarded branch and test both sides.
