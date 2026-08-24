# Primary documentation index

Curated links behind the `dotnet-file-creation` guidance, grouped by the
question they answer.

## Creating and opening

- [File class](https://learn.microsoft.com/dotnet/api/system.io.file)
- [File.Open](https://learn.microsoft.com/dotnet/api/system.io.file.open)
- [File.Create](https://learn.microsoft.com/dotnet/api/system.io.file.create)
- [File.OpenHandle](https://learn.microsoft.com/dotnet/api/system.io.file.openhandle)
- [FileStreamOptions](https://learn.microsoft.com/dotnet/api/system.io.filestreamoptions)
- [FileMode](https://learn.microsoft.com/dotnet/api/system.io.filemode) - `CreateNew` is the atomic create-if-absent
- [FileAccess](https://learn.microsoft.com/dotnet/api/system.io.fileaccess)
- [FileShare](https://learn.microsoft.com/dotnet/api/system.io.fileshare)
- [FileOptions](https://learn.microsoft.com/dotnet/api/system.io.fileoptions) - `DeleteOnClose`, `Asynchronous`, `WriteThrough`
- [RandomAccess](https://learn.microsoft.com/dotnet/api/system.io.randomaccess) - handle-based positional I/O

## Permissions

- [UnixFileMode enum](https://learn.microsoft.com/dotnet/api/system.io.unixfilemode)
- [FileStreamOptions.UnixCreateMode](https://learn.microsoft.com/dotnet/api/system.io.filestreamoptions.unixcreatemode) - the setter carries `[UnsupportedOSPlatform("windows")]`
- [File.GetUnixFileMode](https://learn.microsoft.com/dotnet/api/system.io.file.getunixfilemode)
- [File.SetUnixFileMode](https://learn.microsoft.com/dotnet/api/system.io.file.setunixfilemode)
- [Directory.CreateDirectory(String, UnixFileMode)](https://learn.microsoft.com/dotnet/api/system.io.directory.createdirectory)
- [FileSystemAclExtensions](https://learn.microsoft.com/dotnet/api/system.io.filesystemaclextensions) - the Windows counterpart
- [OperatingSystem.IsWindows](https://learn.microsoft.com/dotnet/api/system.operatingsystem.iswindows) - the guard CA1416 recognizes
- [CA1416: Validate platform compatibility](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1416)
- [Platform compatibility analyzer](https://learn.microsoft.com/dotnet/standard/analyzers/platform-compat-analyzer)
- [Annotating APIs as platform-specific](https://learn.microsoft.com/dotnet/standard/analyzers/platform-compat-analyzer#advanced-scenarios)

## Temporary files

- [Directory.CreateTempSubdirectory](https://learn.microsoft.com/dotnet/api/system.io.directory.createtempsubdirectory) - documents the `700` mode on Unix
- [Path.GetTempPath](https://learn.microsoft.com/dotnet/api/system.io.path.gettemppath)
- [Path.GetTempFileName](https://learn.microsoft.com/dotnet/api/system.io.path.gettempfilename) - the Windows 65,535-name limit was removed in .NET 8
- [Path.GetRandomFileName](https://learn.microsoft.com/dotnet/api/system.io.path.getrandomfilename)

## Where state belongs

- [Environment.SpecialFolder](https://learn.microsoft.com/dotnet/api/system.environment.specialfolder)
- [Environment.GetFolderPath](https://learn.microsoft.com/dotnet/api/system.environment.getfolderpath) - the default option returns an empty string when the directory does not exist; `Create` creates it
- [KNOWNFOLDERID](https://learn.microsoft.com/windows/win32/shell/knownfolderid) - the Windows canonical list
- [XDG Base Directory Specification](https://specifications.freedesktop.org/basedir-spec/latest/) - what the runtime maps to on Linux
- [Isolated storage in multi-user environments](https://learn.microsoft.com/dotnet/standard/io/isolated-storage#impact-in-multi-user-environments)
- [File Access Guide for macOS](https://developer.apple.com/library/archive/documentation/FileManagement/Conceptual/FileSystemProgrammingGuide/FileSystemOverview/FileSystemOverview.html)

## Paths

- [File path formats on Windows systems](https://learn.microsoft.com/dotnet/standard/io/file-path-formats) - normalization, trailing dots and spaces, legacy device names
- [Path class](https://learn.microsoft.com/dotnet/api/system.io.path)
- [Path.Join](https://learn.microsoft.com/dotnet/api/system.io.path.join) - joins without allowing a rooted later segment to replace the root
- [Path.Combine](https://learn.microsoft.com/dotnet/api/system.io.path.combine) - a rooted later segment discards the earlier path; do not use it for path construction
- [Path.IsPathRooted](https://learn.microsoft.com/dotnet/api/system.io.path.ispathrooted) - detects root syntax, including Windows rooted-relative paths
- [Path.IsPathFullyQualified](https://learn.microsoft.com/dotnet/api/system.io.path.ispathfullyqualified) - detects whether current drive or directory state can affect resolution
- [Path.GetFullPath](https://learn.microsoft.com/dotnet/api/system.io.path.getfullpath) - use the overload with a fully qualified base for deterministic relative-path resolution
- [Maximum path length limitation](https://learn.microsoft.com/windows/win32/fileio/maximum-file-path-limitation) - manifest and policy requirements for native Win32 callers
- [Naming files, paths, and namespaces](https://learn.microsoft.com/windows/win32/fileio/naming-a-file)

## Links and reparse points

- [File.CreateSymbolicLink](https://learn.microsoft.com/dotnet/api/system.io.file.createsymboliclink)
- [File.ResolveLinkTarget](https://learn.microsoft.com/dotnet/api/system.io.file.resolvelinktarget)
- [Symbolic link effects on file system functions](https://learn.microsoft.com/windows/win32/fileio/symbolic-link-effects-on-file-systems-functions)
- [Create symbolic links privilege](https://learn.microsoft.com/windows/security/threat-protection/security-policy-settings/create-symbolic-links)

## Moving, replacing, and durability

- [File.Move](https://learn.microsoft.com/dotnet/api/system.io.file.move)
- [File.Replace](https://learn.microsoft.com/dotnet/api/system.io.file.replace) - Windows-oriented; uses a backup file
- [FileStream.Flush(Boolean)](https://learn.microsoft.com/dotnet/api/system.io.filestream.flush)
- [FileStream.SafeFileHandle](https://learn.microsoft.com/dotnet/api/system.io.filestream.safefilehandle)

## Attributes and metadata

- [FileAttributes](https://learn.microsoft.com/dotnet/api/system.io.fileattributes)
- [File.SetAttributes](https://learn.microsoft.com/dotnet/api/system.io.file.setattributes)
- [FileSystemInfo.UnixFileMode](https://learn.microsoft.com/dotnet/api/system.io.filesysteminfo.unixfilemode)

## Secrets

- [ProtectedData class (DPAPI)](https://learn.microsoft.com/dotnet/api/system.security.cryptography.protecteddata) - Windows only
- [Safe storage of app secrets in development](https://learn.microsoft.com/aspnet/core/security/app-secrets)
- [Data Protection in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/data-protection/introduction)

## Background

- [File and stream I/O](https://learn.microsoft.com/dotnet/standard/io/)
- [Common I/O tasks](https://learn.microsoft.com/dotnet/standard/io/common-i-o-tasks)
- [FileStream performance improvements in .NET 6](https://devblogs.microsoft.com/dotnet/file-io-improvements-in-dotnet-6/)
- [Breaking change: FileStream strategy](https://learn.microsoft.com/dotnet/core/compatibility/core-libraries/6.0/filestream-doesnt-allocate-buffer)
- [open(2)](https://man7.org/linux/man-pages/man2/open.2.html) and [umask(2)](https://man7.org/linux/man-pages/man2/umask.2.html) - what the mode argument actually does
