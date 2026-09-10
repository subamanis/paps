#Requires -Version 7.2

[CmdletBinding()]
param(
    [switch] $SkipTools,
    [switch] $CopyProfile
)

$ErrorActionPreference = 'Stop'

$repo = $PSScriptRoot
$powershellHome = Split-Path $PROFILE -Parent
$moduleHome = Join-Path $powershellHome 'Modules'
$scriptHome = Join-Path $powershellHome 'Scripts'

$tools = @(
    @{ Id = 'ajeetdsouza.zoxide';      Command = 'zoxide' }
    @{ Id = 'junegunn.fzf';            Command = 'fzf' }
    @{ Id = 'sharkdp.fd';              Command = 'fd' }
    @{ Id = 'eza-community.eza';       Command = 'eza' }
    @{ Id = 'BurntSushi.ripgrep.MSVC'; Command = 'rg' }
    @{ Id = 'sharkdp.bat';             Command = 'bat' }
)

$galleryModules = @('CompletionPredictor', 'Terminal-Icons')

function Write-Step($text) {
    Write-Host ''
    Write-Host $text -ForegroundColor Cyan
}

function Install-Tool($id, $command) {
    if (Get-Command $command -ErrorAction Ignore) {
        Write-Host "  $command is already here"
        return
    }

    Write-Host "  installing $id"
    winget install --exact --id $id --accept-source-agreements --accept-package-agreements --silent
}

function Install-GalleryModule($name) {
    if (Get-Module $name -ListAvailable) {
        Write-Host "  $name is already here"
        return
    }

    Write-Host "  installing $name"
    Install-Module $name -Scope CurrentUser -Force
}

function Build-Predictor {
    if (-not (Get-Command dotnet -ErrorAction Ignore)) {
        Write-Warning '  dotnet SDK is missing, skipping the predictor'
        return
    }

    $source = Join-Path $repo 'predictor'
    $target = Join-Path $moduleHome 'ContextHistoryPredictor'
    $deployed = Join-Path $target 'ContextHistoryPredictor.dll'

    if (Test-Path $deployed) {
        $newest = Get-ChildItem $source -File -Recurse |
            Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
            Measure-Object LastWriteTimeUtc -Maximum

        if ((Get-Item $deployed).LastWriteTimeUtc -ge $newest.Maximum) {
            Write-Host '  already up to date'
            return
        }
    }

    $project = Join-Path $source 'ContextHistoryPredictor.csproj'
    $output = Join-Path $source 'bin\Release\net8.0\ContextHistoryPredictor.dll'

    dotnet build $project --configuration Release --nologo | Out-Null

    if (-not (Test-Path $output)) {
        throw 'the predictor did not build'
    }

    New-Item -ItemType Directory -Force -Path $target | Out-Null

    try {
        Copy-Item $output $target -Force -ErrorAction Stop
        Copy-Item (Join-Path $source 'ContextHistoryPredictor.psm1') $target -Force
        Copy-Item (Join-Path $source 'ContextHistoryPredictor.psd1') $target -Force
        Write-Host "  built and placed in $target"
    }
    catch [System.IO.IOException] {
        Write-Warning '  another shell has the module loaded, close every PowerShell window and run this again'
    }
}

function Install-File($source, $target) {
    $existing = Get-Item $target -Force -ErrorAction Ignore

    if ($existing) {
        if ($existing.LinkType -eq 'SymbolicLink') {
            if ($existing.LinkTarget -eq $source) {
                Write-Host "  $(Split-Path $target -Leaf) is already linked"
                return
            }

            Remove-Item $target -Force
        }
        else {
            Copy-Item $target "$target.bak" -Force
            Remove-Item $target -Force
        }
    }

    if ($CopyProfile) {
        Copy-Item $source $target -Force
        Write-Host "  copied $(Split-Path $target -Leaf)"
        return
    }

    try {
        New-Item -ItemType SymbolicLink -Path $target -Target $source -Force | Out-Null
        Write-Host "  linked $(Split-Path $target -Leaf)"
    }
    catch {
        Copy-Item $source $target -Force
        Write-Warning "  could not link, copied instead (turn on Developer Mode for links)"
    }
}

if (-not $SkipTools) {
    Write-Step 'Command line tools'

    if (Get-Command winget -ErrorAction Ignore) {
        foreach ($tool in $tools) {
            Install-Tool $tool.Id $tool.Command
        }
    }
    else {
        Write-Warning '  winget is missing, install the tools by hand'
    }
}

Write-Step 'Gallery modules'

foreach ($name in $galleryModules) {
    Install-GalleryModule $name
}

Write-Step 'Context history predictor'

try {
    Build-Predictor
}
catch {
    Write-Warning "  $($_.Exception.Message)"
}

Write-Step 'Profile and scripts'

New-Item -ItemType Directory -Force -Path $powershellHome, $scriptHome | Out-Null

Install-File (Join-Path $repo 'profile\Microsoft.PowerShell_profile.ps1') $PROFILE
Install-File (Join-Path $repo 'scripts\trim-history.ps1') (Join-Path $scriptHome 'trim-history.ps1')

Write-Step 'Done. Open a new window.'
