# Shared machine-wide files

## There is no portable machine-writable location

`Environment.SpecialFolder.CommonApplicationData` looks like the answer. It is
not, because the two platforms mean different things by it:

| | Windows | Linux |
| --- | --- | --- |
| Path | `C:\ProgramData` | `/usr/share` |
| Writable by an ordinary process | Yes, any user can create a top-level entry | No, root-owned |
| Safe to create lazily at first run | **No** | Not possible |

So the same call gives you a directory any standard user can squat on Windows,
and a directory you cannot write at all on Linux. Code that does
`Directory.CreateDirectory(CommonApplicationData + "/YourApp")` and then trusts
the result is wrong on Windows and broken on Linux.

## Decide whether you need shared *storage* or shared *trust*

Most designs that reach for a machine-wide directory want one of these instead:

- **Per-user state that merely happens to be the same for every user.** Use
  per-user storage and accept the duplication. It is almost always cheaper than
  defending a shared location.
- **Read-only content that ships with the product.** Put it beside the
  installation. Writes then require the same privilege the installer had.
- **Small machine-wide configuration.** Use a store the platform already
  protects: the Windows registry under `HKEY_LOCAL_MACHINE`, or a path your
  package provisions on Linux.

If you genuinely need machine-wide *writable* state, it must be provisioned with
the right ownership before any unprivileged code can reach it.

## Provision it at install time

| Platform | Mechanism |
| --- | --- |
| Windows | Windows Installer `MsiLockPermissionsEx` on a created folder, or a registry key under `HKLM\SOFTWARE\<Vendor>\<Product>` |
| Linux | A path under `/var/lib/<app>` or `/etc/<app>` created by the package with an explicit owner and mode |
| macOS | A path under `/Library/Application Support/<app>` created by the installer |

Provisioning removes the first-run race entirely: the location is never absent
while an unprivileged process could create it first.

## If you must create it at run time

Then you own the whole problem, and it is a security problem rather than a file
API problem:

- The privileged process must create the root with its final ownership and
  permissions **at creation**, not create then repair.
- If the root already exists, validate it and **fail closed**. Repairing a
  directory somebody else created leaves their data in place under a descriptor
  that now looks trustworthy.
- Trust the owner, not the permission bits. On both platforms an unprivileged
  caller can produce permissive-looking metadata; only ownership is hard to
  forge.
- Never recursively delete a machine-wide path assembled from untrusted
  components.

On Windows this is involved enough to be its own topic; the Windows ACL skill
covers descriptor creation, root-anchored trust validation, and the deletion
hazards. On Unix the same shape applies with ownership and mode in place of the
descriptor: check `File.GetUnixFileMode` for group and other write bits, and
confirm the owning UID, which requires either a native call or a shelled `stat`
because the BCL does not expose file ownership.

## Sharing between processes, not between users

If the requirement is coordination rather than storage, prefer a mechanism that
does not leave a file behind:

- A named mutex or semaphore for mutual exclusion.
- A lock file opened with `FileShare.None`, kept **inside per-user storage**, for
  cooperating instances of the same user.
- A socket or pipe for actual communication.

A file in a shared directory used as a flag is the pattern most likely to be
hijacked, because its name is predictable and its location is writable by
everyone.

## Reading machine-wide data written by someone else

Even read-only consumption is a trust decision. Anything under a location that
unprivileged users can write is untrusted input:

- Do not let its content select a path your process then writes to or deletes.
- Do not deserialize it into types with side effects.
- Validate it as you would a network payload.

That applies to configuration files, cache manifests, and version markers alike.
