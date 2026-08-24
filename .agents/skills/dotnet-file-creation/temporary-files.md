# Temporary files

## Use a temp subdirectory, not a temp file

`Directory.CreateTempSubdirectory()` gives you a randomized, dedicated scratch
location and makes it owner-only on Unix:

```csharp
DirectoryInfo scratch = Directory.CreateTempSubdirectory("myapp_");
try
{
    string payload = Path.Join(scratch.FullName, "payload.bin");
    // ... work inside scratch ...
}
finally
{
    scratch.Delete(recursive: true);
}
```

- On Unix it is created with owner-only permissions (`700`), which is documented
  and measured. The parent `/tmp` is `777` with the sticky bit, so this is what
  separates your files from every other account on the machine.
- For an interactive Windows user it normally lands under a profile temp
  directory. Windows services are different; see below.

When that directory is private, files you create inside it inherit the
protection, so you do not need to set a mode on each one. That is the main
reason to prefer a directory over individual temp files. On Windows the new
directory inherits the temp root's ACL; service code must not assume that root
is private.

## `Path.GetTempFileName()`

It is not dangerous, but it is limited:

- It creates a zero-byte file with mode `600` on Unix, so permissions are fine.
- On Windows, .NET 7 and earlier failed after 65,535 generated names remained in
  the temp directory. .NET 8 removed that Windows-specific limit.
- It returns a **path, not a handle**, so anything you do next re-resolves the
  name. That is a check-then-use window in a directory other users can write to.
- It always creates in `Path.GetTempPath()`, so you cannot place it beside a
  destination for an atomic rename.

Use it for a quick single-file scratch in a trusted context. Prefer a temp
subdirectory when you need more than one file, an atomic publish, or when
untrusted local users share the machine.

## Never build a predictable temp path

```csharp
// Wrong: on Unix this is a world-writable directory and the name is guessable.
string path = Path.Join(Path.GetTempPath(), "myapp-cache.json");
```

Another user can pre-create that name, or replace it with a symlink pointing
somewhere your process can write. Use a random subdirectory instead, and keep
the predictable name *inside* it.

## Cleaning up

`FileOptions.DeleteOnClose` works on both platforms and removes the file when
the handle closes, including on most abnormal terminations on Windows:

```csharp
using FileStream stream = new(
    path,
    FileMode.CreateNew,
    FileAccess.ReadWrite,
    FileShare.None,
    bufferSize: 4096,
    FileOptions.DeleteOnClose);
```

For a directory, delete it in a `finally`. Accept that a crash can leave one
behind: name it with a recognizable prefix so a later run can sweep old entries
by age, and never recurse into a reparse point while sweeping.

Do not attempt to clean the whole temp root. Other processes and other users
have files there.

## Temp locations differ more than you expect

| | Windows | Unix |
| --- | --- | --- |
| `Path.GetTempPath()` | Interactive users normally get profile `%TEMP%`; services vary | `$TMPDIR` or `/tmp` |
| Mode of that root | Depends on the identity and environment | `777` plus the sticky bit for `/tmp` |
| Shared with other users | Usually not for interactive users; do not assume for services | Yes for `/tmp` |

The sticky bit means another user cannot delete or rename *your* entries, but it
does not stop them creating names before you do or reading anything you leave
world-readable.

## Windows services

On supported Windows versions, current .NET uses `GetTempPath2`. A process
running as `SYSTEM` gets `%SystemRoot%\SystemTemp`, whose ACL grants access to
`SYSTEM` and administrators rather than standard users. Other service identities
still follow `TMP` and `TEMP`; without a loaded profile they can resolve to a
machine-wide location such as `%SystemRoot%\Temp`, where standard users may be
able to create entries.

Test `Path.GetTempPath()` under the service's real identity. Do not infer that it
is private or even writable from behavior in an interactive session.

## Redirected or missing temp

`Path.GetTempPath()` honors `TMPDIR` on Unix and `TMP`/`TEMP` on Windows. In
containers and service accounts these are sometimes unset, pointing at a
read-only location, or shared between users. If your process must work in those
environments, fail with a clear message rather than assuming the path is
writable and private.
