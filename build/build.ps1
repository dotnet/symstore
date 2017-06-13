[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $configuration = "Debug",
  [string] $solution = "",
  [string] $verbosity = "minimal",
  [switch] $restore,
  [switch] $build,
  [switch] $test,
  [switch] $sign,
  [switch] $pack,
  [switch] $ci
)

set-strictmode -version 2.0
$ErrorActionPreference = "Stop"

$RepoRoot = Join-Path $PSScriptRoot "..\"
$DotNetRoot = Join-Path $RepoRoot ".dotnet"
$DotNetExe = Join-Path $DotNetRoot "dotnet.exe"
$BuildProj = Join-Path $PSScriptRoot "build.proj"
$DependenciesProps = Join-Path $PSScriptRoot "Versions.props"
$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$LogDir = Join-Path $ArtifactsDir "log"
$TempDir = Join-Path (Join-Path $ArtifactsDir $configuration) "tmp"

function Create-Directory([string[]] $path) {
  if (!(Test-Path -path $path)) {
    New-Item -path $path -force -itemType "Directory" | Out-Null
  }
}

function GetDotNetCliVersion {
  [xml]$xml = Get-Content $DependenciesProps
  return $xml.Project.PropertyGroup.DotNetCliVersion
}

function InstallDotNetCli {
  
  Create-Directory $DotNetRoot
  $dotnetCliVersion = GetDotNetCliVersion

  $installScript="https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/dotnet-install.ps1"
  Invoke-WebRequest $installScript -OutFile "$DotNetRoot\dotnet-install.ps1"
  
  & "$DotNetRoot\dotnet-install.ps1" -Version $dotnetCliVersion -InstallDir $DotNetRoot
  if ($lastExitCode -ne 0) {
    throw "Failed to install dotnet cli (exit code '$lastExitCode')."
  }
}

function Build {
  $summaryLog = Join-Path $LogDir "Build.log"
  $warningLog = Join-Path $LogDir "Build.wrn"
  $errorLog = Join-Path $LogDir "Build.err"

  Create-Directory($logDir)
  
  & $DotNetExe msbuild $BuildProj /p:Configuration=$configuration /p:SolutionPath=$solution /p:Restore=$restore /p:Build=$build /p:Test=$test /p:Sign=$sign /p:Pack=$pack /p:CIBuild=$ci /v:$verbosity /flp1:Summary`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$summaryLog /flp2:WarningsOnly`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$warningLog /flp3:ErrorsOnly`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$errorLog

  if ($lastExitCode -ne 0) {
    throw "Build failed (exit code '$lastExitCode')."
  }
}

if ($ci) {
  Create-Directory $TempDir
  $env:TEMP = $TempDir
  $env:TMP = $TempDir
}

if ($restore) {
  InstallDotNetCli
}

Build
