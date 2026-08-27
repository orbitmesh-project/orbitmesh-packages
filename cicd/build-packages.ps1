<#
.SYNOPSIS
    Publishes every OrbitMesh package in this repo and zips each one flat, ready to drop into the
    Server's packagesRootDirectory.

.DESCRIPTION
    Produces two artifacts per package from the same dotnet publish output:
    - "<PackageName>.zip" (flat) - the original distribution path, via the Server's own
      packagesRootDirectory + Edge's auto-download (PackageInstance.DownloadPackageAsync).
    - "<PackageName>.<version>.nupkg" - standard NuGet V3, for a feed like Pépite. The publish
      output goes under "content\" inside the package (not "lib\" or "tools\" - see PLAN.md
      discussion: neutral, no install-script/dotnet-tool semantics attached), and the nuspec
      declares <packageType name="OrbitMeshApp" /> so a multi-purpose feed can filter to just
      these. Built via "dotnet pack -p:NuspecFile=..." against a generated .nuspec - despite the
      "lib output" framing in most docs, dotnet pack honors an arbitrary-content nuspec exactly
      like nuget.exe pack does (same NU5100 warnings either way), and it's cross-platform, so no
      vendored nuget.exe/Mono dependency on a Linux build agent.

    Reuses New-ZipArchive from ReleaseHelpers.ps1 - Compress-Archive's backslash-separator bug (see
    release-static-site.ps1's history) would apply here too for any package with subfolders.

.EXAMPLE
    .\build-packages.ps1
    # Builds every package in this repo.

.EXAMPLE
    .\build-packages.ps1 -Only TPLinkSmartHome, Spotify
    # Builds just these two.
#>
param(
    [string]$PackagesDir = "$PSScriptRoot\..",
    [string]$OutputDir = "$PSScriptRoot\build",
    [string]$Runtime = "",
    [string[]]$Only = @(),
    [string]$Authors = "OrbitMesh"
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\ReleaseHelpers.ps1"

function Get-ProjectVersion([string]$ProjectPath) {
    [xml]$csproj = Get-Content $ProjectPath
    $version = ($csproj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
    if (-not $version) { return "1.0.0" }
    return $version
}

function New-OrbitMeshNupkg([string]$Name, [string]$Version, [string]$ProjectPath, [string]$PublishDir, [string]$OutputDir) {
    # Written next to (not inside) $PublishDir, so the glob below never picks up the nuspec itself.
    $nuspecPath = Join-Path $OutputDir "$Name.nuspec"
    $readmePath = Join-Path (Split-Path $ProjectPath -Parent) "README.md"
    # <readme> alone isn't enough - NuGet also needs a matching <file> entry that actually puts the
    # file at that path inside the package (same as <icon>/its own <file> below, if this repo ever
    # gets package icons). Without both, nuget.org/Pépite/the feed's UI has nothing to render.
    $readmeElements = if (Test-Path $readmePath) {
        [PSCustomObject]@{
            Metadata = "    <readme>README.md</readme>"
            File     = "    <file src=`"$readmePath`" target=`"README.md`" />"
        }
    } else {
        Write-Host "No README.md found for $Name at $readmePath - packing without one." -ForegroundColor Yellow
        [PSCustomObject]@{ Metadata = ""; File = "" }
    }
    @"
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata>
    <id>OrbitMesh.$Name</id>
    <version>$Version</version>
    <authors>$Authors</authors>
    <description>OrbitMesh package: $Name</description>
$($readmeElements.Metadata)
    <packageTypes>
      <packageType name="OrbitMeshApp" />
    </packageTypes>
  </metadata>
  <files>
    <file src="$PublishDir\**" target="content" />
$($readmeElements.File)
  </files>
</package>
"@ | Set-Content -Path $nuspecPath -Encoding utf8

    dotnet pack $ProjectPath -c Release --no-build -o $OutputDir -p:NuspecFile=$nuspecPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack failed for $Name (exit code $LASTEXITCODE)"
    }
    Remove-Item $nuspecPath -Force
    return Join-Path $OutputDir "OrbitMesh.$Name.$Version.nupkg"
}

$PackagesDir = (Resolve-Path $PackagesDir).Path
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$OutputDir = (Resolve-Path $OutputDir).Path

$projects = Get-ChildItem $PackagesDir -Directory | ForEach-Object {
    $csproj = Get-ChildItem $_.FullName -Filter "*.csproj" -File | Select-Object -First 1
    if ($csproj) {
        [PSCustomObject]@{ Name = $_.Name; ProjectPath = $csproj.FullName }
    }
}

if ($Only.Count -gt 0) {
    $projects = $projects | Where-Object { $Only -contains $_.Name }
    $missing = $Only | Where-Object { $_ -notin $projects.Name }
    if ($missing.Count -gt 0) {
        throw "No project found for: $($missing -join ', ')"
    }
}

if ($projects.Count -eq 0) {
    throw "No packages found under $PackagesDir"
}

Write-Host "Building $($projects.Count) package(s): $($projects.Name -join ', ')"

$results = @()
foreach ($project in $projects) {
    Write-Step "Building $($project.Name)"
    $publishDir = Join-Path $OutputDir "$($project.Name)-publish"
    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force
    }

    $publishArgs = @($project.ProjectPath, "-c", "Release", "--self-contained", "false", "-o", $publishDir)
    if (-not [string]::IsNullOrWhiteSpace($Runtime)) {
        $publishArgs += @("-r", $Runtime)
    }

    dotnet publish @publishArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED: $($project.Name) (dotnet publish exit code $LASTEXITCODE)" -ForegroundColor Red
        $results += [PSCustomObject]@{ Name = $project.Name; Success = $false; Zip = $null; NuGet = $null }
        continue
    }

    $zipPath = Join-Path $OutputDir "$($project.Name).zip"
    New-ZipArchive -SourceDir $publishDir -DestinationZip $zipPath
    Write-Host "Built: $zipPath" -ForegroundColor Green

    $version = Get-ProjectVersion $project.ProjectPath
    $nupkgPath = New-OrbitMeshNupkg -Name $project.Name -Version $version -ProjectPath $project.ProjectPath -PublishDir $publishDir -OutputDir $OutputDir
    Write-Host "Built: $nupkgPath" -ForegroundColor Green

    $results += [PSCustomObject]@{ Name = $project.Name; Success = $true; Zip = $zipPath; NuGet = $nupkgPath }
}

Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
$results | Format-Table Name, Success, Zip, NuGet -AutoSize

$failed = $results | Where-Object { -not $_.Success }
if ($failed.Count -gt 0) {
    throw "$($failed.Count) package(s) failed to build: $($failed.Name -join ', ')"
}

Write-Host "Copy the .zip(s) above into the Server's packagesRootDirectory, then restart the Edge (or just the affected package) to pick them up."
