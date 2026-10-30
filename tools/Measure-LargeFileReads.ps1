<#
.SYNOPSIS
    Generate deterministic large files or measure large-file read references.
.DESCRIPTION
    Each sample runs in a fresh Release process. Cold samples require elevation
    and purge clean Windows standby pages before launch. This affects the whole
    machine; run only on an otherwise idle benchmark host. Application working
    sets and dirty pages are never emptied. Warm samples follow a full-file warming pass.
    Explicit -ColdPreparation SystemCacheAndStandby additionally trims the system
    file-cache working set before the standby purge, without changing cache
    size-limit settings. It does not empty application working sets.
    Raw samples and source/binary hashes are retained beside the summary.
    A 20 GiB free-space reserve is checked before each child process. Generation
    additionally reserves the requested fixture length before creating the file.
    PublishedHost selects the minimal shared-replay host published as Jit,
    ReadyToRun, or NativeAot. The declared mode is checked against runtime
    identity and, for managed publishes, the app and library R2R headers.
.EXAMPLE
    .\tools\Measure-LargeFileReads.ps1 -Mode Generate -Path C:\bench\offsets.bin
.EXAMPLE
    .\tools\Measure-LargeFileReads.ps1 -Path C:\bench\offsets.bin -Cache Cold,Warm
#>
[CmdletBinding()]
param(
    [ValidateSet("Generate", "Measure")]
    [string]$Mode = "Measure",

    [Parameter(Mandatory)]
    [string]$Path,

    [ValidateRange(1, [long]::MaxValue)]
    [long]$Length = 16GB,

    [ValidateSet("pattern", "blocks")]
    [string]$Pattern = "pattern",

    [ValidateSet("net10.0", "net11.0", "net481")]
    [string[]]$Framework = @("net10.0", "net481"),

    [ValidateSet("Cold", "Warm")]
    [string[]]$Cache = @("Cold", "Warm"),

    [ValidateSet("Standby", "SystemCacheAndStandby")]
    [string]$ColdPreparation = "Standby",

    [ValidateSet("Sequential", "ScriptedSequential", "Sparse", "BlockScan", "EarlyStop")]
    [string[]]$Scenario = @("Sequential", "ScriptedSequential", "Sparse"),

    [ValidateSet("TunedFileStream", "NaiveFileStream", "Raw", "LargeFileStream",
        "RawAsyncHandle", "TunedAsyncHandle", "LargeFileStreamSyncTransport",
        "LargeFileStreamDualTransport", "LargeFileStreamDualTimedTransport")]
    [ValidateNotNullOrEmpty()]
    [string[]]$Reference = @("TunedFileStream", "NaiveFileStream", "Raw"),

    [ValidateRange(5, 100)]
    [int]$Pairs = 5,

    [string]$OutputDirectory,

    [string]$PublishedHost,

    [ValidateSet("Jit", "ReadyToRun", "NativeAot")]
    [string]$CompilationMode,

    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
if ($PublishedHost) {
    if ($Framework.Count -ne 1 -or $Framework -contains "net481" -or -not $CompilationMode) {
        throw "PublishedHost requires one modern Framework and an explicit CompilationMode."
    }

    $PublishedHost = [System.IO.Path]::GetFullPath($PublishedHost)
    if (-not (Test-Path -LiteralPath $PublishedHost -PathType Leaf)) {
        throw "Published host does not exist: $PublishedHost"
    }
}
elseif ($CompilationMode) {
    throw "CompilationMode requires PublishedHost."
}

if ($Mode -eq "Measure" -and $Cache -contains "Cold" -and $ColdPreparation -eq "Standby") {
    Write-Warning "Standby-only purging can retain system-cache pages. Verify physical I/O before calling these results cold."
}

$modernReferences = @("LargeFileStream", "RawAsyncHandle", "TunedAsyncHandle",
    "LargeFileStreamSyncTransport", "LargeFileStreamDualTransport", "LargeFileStreamDualTimedTransport")
if ($Mode -eq "Measure" -and $Framework -contains "net481" -and
    @($Reference | Where-Object { $_ -in $modernReferences }).Count -gt 0) {
    throw "The candidate and diagnostic controls are modern-.NET-only. Select net10.0 or net11.0."
}

$repo = Split-Path -Parent $PSScriptRoot
$Path = [System.IO.Path]::GetFullPath($Path)
if ($Mode -eq "Measure" -and -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "Reference file does not exist: $Path"
}

if ($Mode -eq "Generate" -and (Test-Path -LiteralPath $Path)) {
    throw "Refusing to overwrite '$Path'. Choose a new file name."
}

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repo "artifacts\large-file-reads\$([DateTime]::UtcNow.ToString('yyyyMMddTHHmmss'))-$([Guid]::NewGuid().ToString('N'))"
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$minimumFreeSpace = 20GB
$driveRoots = @([System.IO.Path]::GetPathRoot($Path), [System.IO.Path]::GetPathRoot($OutputDirectory)) |
    Select-Object -Unique

function Assert-FreeDiskSpace {
    foreach ($root in $driveRoots) {
        $drive = [System.IO.DriveInfo]::new($root)
        if ($drive.AvailableFreeSpace -lt $minimumFreeSpace) {
            throw "Less than 20 GiB free on '$root'. Stopping without deleting retained results."
        }
    }
}

function Assert-FixtureDiskSpace {
    $fixtureDrive = [System.IO.DriveInfo]::new([System.IO.Path]::GetPathRoot($Path))
    if ($Length -gt $fixtureDrive.AvailableFreeSpace - $minimumFreeSpace) {
        throw "The requested fixture would leave less than 20 GiB free. Choose a smaller fixture or another volume."
    }
}

Assert-FreeDiskSpace
if ($Mode -eq "Generate") {
    Assert-FixtureDiskSpace
}

if (Test-Path -LiteralPath $OutputDirectory) {
    throw "Use a new output directory to avoid mixing runs: $OutputDirectory"
}

New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$project = Join-Path $repo "test\touki.perf\touki.perf.csproj"
$executables = @{}
$hashes = @{}
$applicationHashes = @{}
$libraryHashes = @{}
$columns = "Length,Milliseconds,CpuMilliseconds,AllocatedBytes,BackendBytesRead,RequestsIssued,OwnedPayloadBytes,FirstByteMilliseconds,Checksum"

function Invoke-ReferenceHost([string]$Target, [string[]]$HostArguments) {
    Assert-FreeDiskSpace
    if ($HostArguments[0] -eq "generate") {
        Assert-FixtureDiskSpace
    }

    $executable = $executables[$Target]
    if ($PublishedHost -or $Target -eq "net481") {
        $output = & $executable --large-file @HostArguments
    }
    else {
        $output = & dotnet $executable --large-file @HostArguments
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Large-file host failed ($Target, $HostArguments), exit $LASTEXITCODE."
    }

    return $output
}

Push-Location $repo
try {
    foreach ($target in $Framework) {
        if (-not $SkipBuild -and -not $PublishedHost) {
            dotnet build $project -c Release -f $target --nologo
            if ($LASTEXITCODE -ne 0) {
                throw "Release build failed for $target."
            }
        }

        $extension = if ($target -eq "net481") { "exe" } else { "dll" }
        $executable = if ($PublishedHost) {
            $PublishedHost
        }
        else {
            Join-Path $repo "artifacts\x64\Release\touki.perf\$target\touki.perf.$extension"
        }

        if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
            throw "Release executable not found: $executable"
        }

        $executables[$target] = $executable
        $hashes[$target] = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
        if ($PublishedHost -and $CompilationMode -ne "NativeAot") {
            $applicationAssembly = [System.IO.Path]::ChangeExtension($executable, ".dll")
            $applicationHashes[$target] = (Get-FileHash -LiteralPath $applicationAssembly -Algorithm SHA256).Hash
        }

        $library = Join-Path (Split-Path -Parent $executable) "touki.dll"
        if (Test-Path -LiteralPath $library) {
            $libraryHashes[$target] = (Get-FileHash -LiteralPath $library -Algorithm SHA256).Hash
        }
        elseif ($CompilationMode -ne "NativeAot") {
            throw "The published managed host is missing touki.dll: $library"
        }
    }

    $hostRuntime = $null
    if ($PublishedHost) {
        $hostRuntime = & $PublishedHost --runtime-info
        if ($LASTEXITCODE -ne 0) {
            throw "Cannot inspect published host runtime."
        }

        $native = "$hostRuntime" -like "*DynamicCodeSupported=False*DynamicCodeCompiled=False*"
        if ($native -ne ($CompilationMode -eq "NativeAot")) {
            throw "Published host runtime does not match CompilationMode: $hostRuntime"
        }

        $runtimePrefix = if ($Framework[0] -eq "net10.0") { ".NET 10." } else { ".NET 11." }
        if (-not "$hostRuntime".StartsWith($runtimePrefix, [StringComparison]::Ordinal)) {
            throw "Published host runtime does not match Framework: $hostRuntime"
        }

        $readyToRunSetting = if ($env:DOTNET_ReadyToRun) { $env:DOTNET_ReadyToRun } else { $env:COMPlus_ReadyToRun }
        if ($CompilationMode -eq "ReadyToRun" -and $readyToRunSetting -eq "0") {
            throw "The ReadyToRun environment setting would disable the published R2R code."
        }

        if (-not $native) {
            $appAssembly = [System.IO.Path]::ChangeExtension($PublishedHost, ".dll")
            foreach ($module in @($appAssembly, (Join-Path (Split-Path -Parent $PublishedHost) "touki.dll"))) {
                $stream = [System.IO.File]::OpenRead($module)
                $reader = [System.Reflection.PortableExecutable.PEReader]::new($stream)
                try {
                    $ready = $reader.PEHeaders.CorHeader.ManagedNativeHeaderDirectory.Size -gt 0
                    if ($ready -ne ($CompilationMode -eq "ReadyToRun")) {
                        throw "R2R header does not match CompilationMode in '$module'."
                    }
                }
                finally {
                    $reader.Dispose()
                    $stream.Dispose()
                }
            }
        }
    }

    if ($Mode -eq "Generate") {
        Invoke-ReferenceHost $Framework[0] @("generate", $Path, $Length.ToString([cultureinfo]::InvariantCulture), $Pattern)
        Invoke-ReferenceHost $Framework[0] @("verify", $Path, $Pattern)
    }
    else {
        # Reject corrupted or wrong-pattern inputs before timing, not by checksum.
        Invoke-ReferenceHost $Framework[0] @("verify", $Path, $Pattern)
    }

    $revision = git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot read source revision."
    }

    $status = @(git status --porcelain=v1)
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot read source status."
    }

    $sources = @(Get-ChildItem (Join-Path $repo "test\touki.perf\LargeFile*.cs"))
    $sources += Get-Item -LiteralPath $PSCommandPath
    if (-not $PublishedHost) {
        $sources += Get-Item -LiteralPath (Join-Path $repo "test\touki.perf\Program.cs")
    }
    $sources += Get-Item -LiteralPath (Join-Path $repo "touki\NativeMethods.txt")
    if ($PublishedHost) {
        $sources += Get-ChildItem (Join-Path $repo "test\touki.largefileprobe\*.cs")
        $sources += Get-Item -LiteralPath (Join-Path $repo "test\touki.largefileprobe\touki.largefileprobe.csproj")
    }

    if ($Reference -contains "LargeFileStream" -or $Reference -contains "LargeFileStreamSyncTransport" -or
        $Reference -contains "LargeFileStreamDualTransport" -or $Reference -contains "LargeFileStreamDualTimedTransport") {
        $sources += Get-ChildItem (Join-Path $repo "touki\Touki\Io\LargeFile*.cs")
        $sources += Get-ChildItem (Join-Path $repo "touki\Touki\Io\RandomAccessFileReadBackend*.cs")
        $sources += Get-Item -LiteralPath (Join-Path $repo "touki\Touki\Io\IFileReadBackend.cs"),
            (Join-Path $repo "touki\Modern.NativeMethods.txt"),
            (Join-Path $repo "touki\touki.csproj")
    }

    $sourceHashes = @{}
    foreach ($source in $sources) {
        $sourceHashes[$source.Name] = (Get-FileHash -LiteralPath $source.FullName -Algorithm SHA256).Hash
        Copy-Item -LiteralPath $source.FullName -Destination $OutputDirectory
    }

    [ordered]@{
        CreatedUtc = [DateTime]::UtcNow.ToString("O")
        Revision = "$revision"
        Status = $status
        SourceSha256 = $sourceHashes
        BinarySha256 = $hashes
        ApplicationAssemblySha256 = $applicationHashes
        LibrarySha256 = $libraryHashes
        PublishedHost = $PublishedHost
        CompilationMode = $CompilationMode
        HostRuntime = $hostRuntime
        LibraryEmbeddedInNativeHost = $CompilationMode -eq "NativeAot"
        ReadyToRunEnvironment = $env:DOTNET_ReadyToRun
        TieredCompilationEnvironment = $env:DOTNET_TieredCompilation
        ComPlusReadyToRunEnvironment = $env:COMPlus_ReadyToRun
        ComPlusTieredCompilationEnvironment = $env:COMPlus_TieredCompilation
        Sdk = "$(dotnet --version)"
        OS = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
        Processor = $env:PROCESSOR_IDENTIFIER
        File = $Path
        Length = (Get-Item -LiteralPath $Path).Length
        FileSha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
        Pattern = $Pattern
        Frameworks = $Framework
        Scenarios = $Scenario
        References = $Reference
        Cache = $Cache
        Pairs = $Pairs
        MinimumFreeSpaceBytes = $minimumFreeSpace
        ColdMethod = $ColdPreparation
        ColdMethodDescription = "Optional system file-cache working-set trim, then MemoryPurgeStandbyList; fresh child per sample"
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory "manifest.json")

    if ($Mode -eq "Generate") {
        Write-Host "Generated and byte-verified $Path. Manifest: $OutputDirectory"
        return
    }

    $samplesPath = Join-Path $OutputDirectory "samples.csv"
    "Framework,Scenario,Cache,Pair,Order,Reference,$columns" | Set-Content -LiteralPath $samplesPath
    foreach ($target in $Framework) {
        foreach ($workload in $Scenario) {
            foreach ($cacheMode in $Cache) {
                $expectedChecksum = $null
                for ($pair = 1; $pair -le $Pairs; $pair++) {
                    $references = @($Reference)
                    if ($pair % 2 -eq 0) {
                        [array]::Reverse($references)
                    }

                    $order = 0
                    foreach ($referenceName in $references) {
                        if ($cacheMode -eq "Cold") {
                            $purgeCommand = if ($ColdPreparation -eq "SystemCacheAndStandby") {
                                "purge-system-cache"
                            }
                            else {
                                "purge"
                            }

                            Invoke-ReferenceHost $target @($purgeCommand)
                        }
                        else {
                            $null = Invoke-ReferenceHost $target @("run", $Path, "TunedFileStream", "Sequential")
                        }

                        $sample = @(Invoke-ReferenceHost $target @("run", $Path, $referenceName, $workload))
                        if ($sample.Count -ne 1) {
                            throw "Expected one CSV sample from host; received $($sample.Count)."
                        }

                        $parsed = $sample[0] | ConvertFrom-Csv -Header ($columns -split ",")
                        if (-not $parsed.Checksum -or [double]::Parse($parsed.Milliseconds, [cultureinfo]::InvariantCulture) -le 0) {
                            throw "Malformed sample: $sample"
                        }

                        if ($null -eq $expectedChecksum) {
                            $expectedChecksum = $parsed.Checksum
                        }
                        elseif ($parsed.Checksum -ne $expectedChecksum) {
                            throw "Replay result changed for $target / $workload / $referenceName."
                        }

                        $order++
                        "$target,$workload,$cacheMode,$pair,$order,$referenceName,$($sample[0])" |
                            Add-Content -LiteralPath $samplesPath
                        Write-Host "$target $workload $cacheMode pair $pair $referenceName : $($parsed.Milliseconds) ms"
                    }
                }
            }
        }
    }

    $samples = @(Import-Csv -LiteralPath $samplesPath)
    $expectedSampleCount = $Framework.Count * $Scenario.Count * $Cache.Count * $Pairs * $Reference.Count
    if ($samples.Count -ne $expectedSampleCount) {
        throw "Expected $expectedSampleCount samples; collected $($samples.Count)."
    }

    $summary = $samples |
        Group-Object Framework,Scenario,Cache,Reference |
        ForEach-Object {
            $values = @($_.Group | ForEach-Object {
                [double]::Parse($_.Milliseconds, [cultureinfo]::InvariantCulture)
            })
            $mean = ($values | Measure-Object -Average).Average
            $squared = ($values | ForEach-Object { [Math]::Pow($_ - $mean, 2) } | Measure-Object -Sum).Sum
            [pscustomobject]@{
                Framework = $_.Group[0].Framework
                Scenario = $_.Group[0].Scenario
                Cache = $_.Group[0].Cache
                Reference = $_.Group[0].Reference
                Samples = $values.Count
                MeanMilliseconds = $mean
                StdDevMilliseconds = [Math]::Sqrt($squared / ($values.Count - 1))
                MinMilliseconds = ($values | Measure-Object -Minimum).Minimum
                MaxMilliseconds = ($values | Measure-Object -Maximum).Maximum
            }
        }
    $summary | Export-Csv -LiteralPath (Join-Path $OutputDirectory "summary.csv") -NoTypeInformation
    $summary | Format-Table -AutoSize
    Write-Host "Retained samples and provenance: $OutputDirectory"
}
finally {
    Pop-Location
}
