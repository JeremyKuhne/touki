# Copyright (c) 2025 Jeremy W Kuhne
# SPDX-License-Identifier: MIT
# See LICENSE file in the project root for full license information

<#
.SYNOPSIS
    Ensures the prerequisites for running the touki.fuzz coverage-guided fuzzing
    harness are installed, installing any that are missing.

.DESCRIPTION
    Checks for and installs, in order:

      1. .NET 8+ SDK            - required by the SharpFuzz instrumentation tool.
                                  Not auto-installed (large); the script fails
                                  with guidance if it is missing.
      2. SharpFuzz.CommandLine  - global .NET tool that instruments assemblies.
                                  Installed per-user (no elevation needed).
      3. libfuzzer-dotnet.exe   - the native libFuzzer driver. Downloaded as a
                                  prebuilt, per-platform release binary into
                                  tools/ (no compiler or elevation required).

    The prebuilt driver is published by the upstream libfuzzer-dotnet project
    for Windows, Ubuntu, and Debian, so no clang/LLVM toolchain is needed.

    Re-running is safe: present prerequisites are reported and skipped unless
    -Force is supplied.

.PARAMETER Force
    Reinstall / rebuild prerequisites even when they are already present.

.EXAMPLE
    pwsh touki.fuzz/Install-FuzzPrereqs.ps1

.EXAMPLE
    pwsh touki.fuzz/Install-FuzzPrereqs.ps1 -Force
#>

[CmdletBinding()]
param(
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$MinimumSdkMajor = 8
$LibFuzzerRelease = 'v2025.05.02.0904'
$LibFuzzerReleaseBase = "https://github.com/Metalnem/libfuzzer-dotnet/releases/download/$LibFuzzerRelease"
$ToolsDirectory = Join-Path $PSScriptRoot 'tools'
$LibFuzzerExe = Join-Path $ToolsDirectory 'libfuzzer-dotnet.exe'

function Write-Step {
    param([string]$Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "    $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "    $Message" -ForegroundColor Yellow
}

function Test-DotNetSdk {
    Write-Step ".NET 8+ SDK"

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "The 'dotnet' CLI was not found. Install the .NET 8+ SDK from https://dotnet.microsoft.com/download and re-run."
    }

    $installed = dotnet --list-sdks |
        ForEach-Object { ($_ -split ' ')[0] } |
        Where-Object { $_ } |
        ForEach-Object { [int]($_ -split '\.')[0] }

    $highest = ($installed | Measure-Object -Maximum).Maximum

    if ($highest -lt $MinimumSdkMajor) {
        throw "A .NET $MinimumSdkMajor+ SDK is required by SharpFuzz instrumentation; highest found is major version $highest. Install from https://dotnet.microsoft.com/download."
    }

    Write-Ok "Found .NET SDK major version $highest."
}

function Install-SharpFuzzTool {
    Write-Step "SharpFuzz.CommandLine global tool"

    $installed = (dotnet tool list --global 2>$null) -match 'sharpfuzz\.commandline'

    if ($installed -and -not $Force) {
        Write-Ok "Already installed."
        return
    }

    if ($installed -and $Force) {
        Write-Warn "Reinstalling (-Force)..."
        dotnet tool update --global SharpFuzz.CommandLine | Out-Host
    }
    else {
        dotnet tool install --global SharpFuzz.CommandLine | Out-Host
    }

    Write-Ok "SharpFuzz.CommandLine ready."
}

function Get-LibFuzzerAssetName {
    if ($IsWindows) {
        return 'libfuzzer-dotnet-windows.exe'
    }

    # The upstream project ships Ubuntu and Debian builds. Prefer the Debian
    # build on Debian, Ubuntu everywhere else (the Ubuntu binary also runs on
    # most other glibc distributions).
    if ((Test-Path '/etc/os-release') -and (Select-String -Path '/etc/os-release' -Pattern '^ID=debian' -Quiet)) {
        return 'libfuzzer-dotnet-debian'
    }

    return 'libfuzzer-dotnet-ubuntu'
}

function Install-LibFuzzerDriver {
    Write-Step "libfuzzer-dotnet driver"

    if ((Test-Path $LibFuzzerExe) -and -not $Force) {
        Write-Ok "Already present at $LibFuzzerExe."
        return
    }

    if (-not (Test-Path $ToolsDirectory)) {
        New-Item -ItemType Directory -Path $ToolsDirectory | Out-Null
    }

    $asset = Get-LibFuzzerAssetName
    $url = "$LibFuzzerReleaseBase/$asset"

    Write-Warn "Downloading prebuilt driver ($asset, $LibFuzzerRelease)..."
    Invoke-WebRequest -Uri $url -OutFile $LibFuzzerExe

    # The driver is invoked by path, so the .exe extension is harmless on Unix;
    # mark it executable there so it can be run directly.
    if (-not $IsWindows) {
        & chmod +x $LibFuzzerExe
    }

    Write-Ok "Driver ready at $LibFuzzerExe."
}

Write-Host ""
Write-Host "touki.fuzz prerequisite check" -ForegroundColor White
Write-Host "-----------------------------" -ForegroundColor White

Test-DotNetSdk
Install-SharpFuzzTool
Install-LibFuzzerDriver

Write-Host ""
Write-Host "All prerequisites are ready." -ForegroundColor Green
Write-Host "See touki.fuzz/README.md for the instrument-and-run workflow." -ForegroundColor Green
