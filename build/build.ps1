[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $configuration = "Debug",
  [string] $solution = "",
  [string] $verbosity = "minimal",
  [switch] $restore,
  [switch] $build,
  [switch] $rebuild,
  [switch] $test,
  [switch] $sign,
  [switch] $pack,
  [switch] $ci,
  [switch] $prepareMachine,
  [switch] $log,
  [switch] $help,
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

set-strictmode -version 2.0
$ErrorActionPreference = "Stop"

function Print-Usage() {
    Write-Host "Common settings:"
    Write-Host "  -configuration <value>  Build configuration Debug, Release"
    Write-Host "  -verbosity <value>      Msbuild verbosity (q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic])"
    Write-Host "  -help                   Print help and exit"
    Write-Host ""

    Write-Host "Actions:"
    Write-Host "  -restore                Restore dependencies"
    Write-Host "  -build                  Build solution"
    Write-Host "  -rebuild                Rebuild solution"
    Write-Host "  -test                   Run all unit tests in the solution"
    Write-Host "  -sign                   Sign build outputs"
    Write-Host "  -pack                   Package build outputs into NuGet packages and Willow components"
    Write-Host ""

    Write-Host "Advanced settings:"
    Write-Host "  -solution <value>       Path to solution to build"
    Write-Host "  -ci                     Set when running on CI server"
    Write-Host "  -log                    Enable logging (by default on CI)"
    Write-Host "  -prepareMachine         Prepare machine for CI run"
    Write-Host ""
    Write-Host "Command line arguments not listed above are passed thru to msbuild."
    Write-Host "The above arguments can be shortened as much as to be unambiguous (e.g. -co for configuration, -t for test, etc.)."
}

if ($help -or (($properties -ne $null) -and ($properties.Contains("/help") -or $properties.Contains("/?")))) {
  Print-Usage
  exit 0
}

function Create-Directory([string[]] $path) {
  if (!(Test-Path $path)) {
    New-Item -path $path -force -itemType "Directory" | Out-Null
  }
}

function GetVersion([string] $name) {
  foreach ($propertyGroup in $VersionsXml.Project.PropertyGroup) {
    if (Get-Member -inputObject $propertyGroup -name $name) {
        return $propertyGroup.$name
    }
  }

  throw "Failed to find $name in Versions.props"
}

function InstallDotNetCli {
  
  Create-Directory $DotNetRoot
  $dotnetCliVersion = GetVersion("DotNetCliVersion")

  $installScript = "$DotNetRoot\dotnet-install.ps1"
  if (!(Test-Path $installScript)) { 
    Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript
  }
  
  & $installScript -Version $dotnetCliVersion -InstallDir $DotNetRoot
  if ($lastExitCode -ne 0) {
    throw "Failed to install dotnet cli (exit code '$lastExitCode')."
  }
}

function InstallToolset {
  if (!(Test-Path $ToolsetBuildProj)) {
    & $DotNetExe msbuild $ToolsetRestoreProj /t:restore /m /nologo /clp:Summary /warnaserror /v:$verbosity /p:NuGetPackageRoot=$NuGetPackageRoot /p:BaseIntermediateOutputPath=$ToolsetDir /p:ExcludeRestorePackageImports=true
  }
}

function InstallVersionOverrides {  
  if ($env:PB_PackageVersionPropsUrl -ne $null) {
    Create-Directory $ToolsetDir
    Write-Host "Downloading $env:PB_PackageVersionPropsUrl ..."
    Invoke-WebRequest $env:PB_PackageVersionPropsUrl -OutFile "$ToolsetDir\PackageVersionOverrides.props"
  }
}

function Build {
  if ($ci -or $log) {
    Create-Directory($logDir)
    $logCmd = "/bl:" + (Join-Path $LogDir "Build.binlog")
  } else {
    $logCmd = ""
  }
 
  & $DotNetExe msbuild $ToolsetBuildProj /m /nologo /clp:Summary /warnaserror /v:$verbosity $logCmd /p:RepoRoot=$RepoRoot /p:Configuration=$configuration /p:SolutionPath=$solution /p:Restore=$restore /p:Build=$build /p:Rebuild=$rebuild /p:Test=$test /p:Sign=$sign /p:Pack=$pack /p:CIBuild=$ci /p:NuGetPackageRoot=$NuGetPackageRoot $properties
}

function Stop-Processes() {
  Write-Host "Killing running build processes..."
  Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Stop-Process
  Get-Process -Name "vbcscompiler" -ErrorAction SilentlyContinue | Stop-Process
}

try {

  $RepoRoot = Join-Path $PSScriptRoot "..\"
  $DotNetRoot = Join-Path $RepoRoot ".dotnet"
  $DotNetExe = Join-Path $DotNetRoot "dotnet.exe"
  $BuildProj = Join-Path $PSScriptRoot "build.proj"
  $ToolsetRestoreProj = Join-Path $PSScriptRoot "Toolset.proj"
  $ArtifactsDir = Join-Path $RepoRoot "artifacts"
  $ToolsetDir = Join-Path $ArtifactsDir "toolset"
  $LogDir = Join-Path (Join-Path $ArtifactsDir $configuration) "log"
  $TempDir = Join-Path (Join-Path $ArtifactsDir $configuration) "tmp"
  [xml]$VersionsXml = Get-Content(Join-Path $PSScriptRoot "Versions.props")

  if ($solution -eq "") {
    $solution = @(gci(Join-Path $RepoRoot "*.sln"))[0]
  }

  if ($env:NUGET_PACKAGES -ne $null) {
    $NuGetPackageRoot = $env:NUGET_PACKAGES.TrimEnd("\") + "\"
  } else {
    $NuGetPackageRoot = Join-Path $env:UserProfile ".nuget\packages\"
  }

  $ToolsetVersion = GetVersion("RoslynToolsRepoToolsetVersion")
  $ToolsetBuildProj = Join-Path $NuGetPackageRoot "RoslynTools.RepoToolset\$ToolsetVersion\tools\Build.proj"

  if ($ci) {
    Create-Directory $TempDir
    $env:TEMP = $TempDir
    $env:TMP = $TempDir
  }

  if ($restore) {
    InstallVersionOverrides
    InstallDotNetCli
    InstallToolset
  }

  Build
  exit $lastExitCode
}
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  exit 1
}
finally {
  Pop-Location
  if ($ci -and $prepareMachine) {
    Stop-Processes
  }
}

