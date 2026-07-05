<#
.SYNOPSIS
    Verify that the managed assemblies inside a .nupkg are architecture neutral.

.DESCRIPTION
    KlutzyNinja.Touki and KlutzyNinja.Touki.TestSupport are pure-IL libraries with
    no native components, so every assembly they ship must be buildable as
    "AnyCPU" (PE machine type I386 with the ILOnly CorFlag and without the
    32BitRequired flag). A project that accidentally inherits the repo-wide
    Platform=x64 dev default (see Directory.Build.props / touki.slnx) instead
    gets tagged AMD64 (PE32Plus), which throws BadImageFormatException when
    loaded into a non-x64 process - e.g. a native ARM64 .NET host on Windows,
    Linux, or Apple Silicon macOS, or an ARM64 Visual Studio / dotnet CLI
    analyzer host.

    This script extracts every *.dll from the given .nupkg and fails if any of
    them are not architecture neutral.

.PARAMETER PackagePath
    Path to the .nupkg file to inspect.

.EXAMPLE
    pwsh tools/Test-PackageArchitectureNeutral.ps1 -PackagePath ./artifacts/packages/KlutzyNinja.Touki.1.0.0.nupkg
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PackagePath
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression.FileSystem

$PackagePath = (Resolve-Path $PackagePath).Path
$extractDir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
New-Item -ItemType Directory -Path $extractDir | Out-Null

try {
    [System.IO.Compression.ZipFile]::ExtractToDirectory($PackagePath, $extractDir)

    $dlls = @(Get-ChildItem -Path $extractDir -Recurse -Filter '*.dll')
    if ($dlls.Count -eq 0) {
        Write-Error "No assemblies found inside '$PackagePath'."
    }

    $failures = @()
    foreach ($dll in $dlls) {
        $relativePath = $dll.FullName.Substring($extractDir.Length + 1)
        $bytes = [System.IO.File]::ReadAllBytes($dll.FullName)
        $stream = [System.IO.MemoryStream]::new($bytes)
        try {
            $peReader = [System.Reflection.PortableExecutable.PEReader]::new($stream)
            try {
                $headers = $peReader.PEHeaders
                $machine = $headers.CoffHeader.Machine
                $corHeader = $headers.CorHeader

                if ($null -eq $corHeader) {
                    $failures += "$relativePath -> not a managed (CLI) assembly"
                    continue
                }

                $isILOnly = [bool]($corHeader.Flags -band [System.Reflection.PortableExecutable.CorFlags]::ILOnly)
                $requires32Bit = [bool]($corHeader.Flags -band [System.Reflection.PortableExecutable.CorFlags]::Requires32Bit)

                if ($machine -ne [System.Reflection.PortableExecutable.Machine]::I386 -or -not $isILOnly -or $requires32Bit) {
                    $failures += "$relativePath -> Machine=$machine ILOnly=$isILOnly Requires32Bit=$requires32Bit"
                }
                else {
                    Write-Host "OK: $relativePath -> Machine=$machine"
                }
            }
            finally {
                $peReader.Dispose()
            }
        }
        finally {
            $stream.Dispose()
        }
    }

    if ($failures.Count -gt 0) {
        Write-Error "Found architecture-specific assemblies in '$PackagePath':`n$($failures -join "`n")"
    }

    Write-Host "All assemblies in '$PackagePath' are architecture neutral."
}
finally {
    Remove-Item -Recurse -Force $extractDir -ErrorAction SilentlyContinue
}
