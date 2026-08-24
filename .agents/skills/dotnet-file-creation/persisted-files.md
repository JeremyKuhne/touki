# Persisted per-user files

## Choose the folder by lifetime, not by name

| Use | `Environment.SpecialFolder` | Windows | Linux |
| --- | --- | --- | --- |
| Settings that follow the user between machines | `ApplicationData` | `%AppData%` | `$XDG_CONFIG_HOME` or `~/.config` |
| Caches, logs, indexes, downloaded payloads | `LocalApplicationData` | `%LocalAppData%` | `$XDG_DATA_HOME` or `~/.local/share` |
| The profile root itself | `UserProfile` | `C:\Users\<user>` | `/home/<user>` |

With the default mapping, both per-user folders resolve under the user profile.
On XDG-based Unix systems, absolute `XDG_CONFIG_HOME` and `XDG_DATA_HOME`
values can redirect them elsewhere.

The default `GetFolderPath` option verifies that the directory exists and
returns an empty string when it does not. That is common on fresh Unix accounts
and minimal containers. Never pass the unchecked result to `Path.Join`: joining
`""` and `"YourApp"` produces a relative path in the current directory. Request
creation, then still reject an empty result:

Prefer `LocalApplicationData` unless the data genuinely should roam. Roaming
profiles copy `ApplicationData` at logon and logoff, so a large cache there is a
logon delay for the user.

## Per-user does not mean private on Unix

This is the trap. One Ubuntu environment with a default `umask` of `022`
measured:

```text
~/.config                        mode 755
~/.config/yourapp                mode 755   (plain Directory.CreateDirectory)
~/.config/yourapp/settings.json  mode 644   (plain File.WriteAllText)
reachable and readable by other local users: True
```

Those parent modes are measurements, not Linux guarantees. Desktop tooling can
create `~/.config` as `700`; `~/.local/share` is commonly `755`. Set the mode on
your application directory instead of depending on either parent.

On Windows the equivalent path is restricted to the user, `SYSTEM`, and
administrators, so the same code is private there and exposed on Linux. Tokens,
refresh cookies, and personal data written this way leak to every account on a
shared machine.

## The fix: create the directory owner-only, then the files

```csharp
string dataDirectory = Environment.GetFolderPath(
    Environment.SpecialFolder.LocalApplicationData,
    Environment.SpecialFolderOption.Create);
if (string.IsNullOrEmpty(dataDirectory))
{
    throw new InvalidOperationException("The per-user data directory is unavailable.");
}

string root = Path.Join(dataDirectory, "YourApp");

if (OperatingSystem.IsWindows())
{
    Directory.CreateDirectory(root);
}
else
{
    Directory.CreateDirectory(
        root,
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
}
```

Two cautions, both measured:

- The mode applies to the **leaf only**. If `YourApp/Cache/v2` has to create
  `YourApp` and `Cache` along the way, those get the process default (`755` under
  a `022` umask), not the mode you asked for. Create each level you care about in
  its own call.
- `Directory.CreateDirectory` on an existing directory does not change its mode.
  If a previous version of your app created it `755`, it stays `755`. Decide
  deliberately whether to tighten it with `File.SetUnixFileMode` on upgrade.

Once the directory is `700`, files created inside it are unreachable by other
users even at `644`, because traversal requires execute permission on the
directory. Setting `600` on the files as well is still worth doing when the data
is sensitive, so a later permission change on the directory does not expose them.

## Writing the file

Publish atomically so a reader never sees a partial file and a crash never
destroys the previous good copy:

```csharp
string finalPath = Path.Join(root, "settings.json");
string temporary = Path.Join(root, $".settings.json.{Guid.NewGuid():N}.tmp");

var options = new FileStreamOptions
{
    Mode = FileMode.CreateNew,
    Access = FileAccess.Write,
};

if (!OperatingSystem.IsWindows())
{
    options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
}

try
{
    await using (FileStream stream = File.Open(temporary, options))
    {
        await JsonSerializer.SerializeAsync(stream, settings);
        stream.Flush(flushToDisk: true);
    }

    File.Move(temporary, finalPath, overwrite: true);
}
catch
{
    if (File.Exists(temporary)) { File.Delete(temporary); }
    throw;
}
```

Points that matter:

- The temporary file is in the **same directory**, so the move is a rename inside
  one volume. Across volumes `File.Move` degrades to copy-then-delete and stops
  being atomic.
- `Flush(flushToDisk: true)` before the rename. Without it a power loss can leave
  the renamed file present but empty.
- A leading dot keeps the temporary out of casual listings on Unix and marks it
  as ignorable to your own readers.

## Concurrency between your own processes

Two instances of your application writing the same file is the common case, not
an edge case. `FileShare` is enforced by Windows. On Unix, .NET takes advisory
`flock` locks, so a lock file coordinates processes that take compatible locks:

```csharp
using FileStream gate = File.Open(
    Path.Join(root, ".lock"),
    new FileStreamOptions
    {
        Mode = FileMode.OpenOrCreate,
        Access = FileAccess.ReadWrite,
        Share = FileShare.None,
    });
```

A native process that also takes `flock` participates; any process can ignore
the advisory lock. `DOTNET_SYSTEM_IO_DISABLEFILELOCKING=1` disables .NET file
locking on Unix entirely. Treat the lock as coordination between cooperating
instances, not as a security boundary.

## Do not put secrets here

ACLs and modes keep other *users* out. They do not keep an administrator or root
out, and they do not protect a file at rest. For tokens and credentials use
DPAPI (`ProtectedData`) on Windows, the platform keychain or secret service
elsewhere, or do not persist the secret at all.
