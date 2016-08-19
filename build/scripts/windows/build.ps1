[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $configuration = "Debug",
  [string] $deployHive = "TestImpact",
  [string] $msbuildVersion = "14.0",
  [string] $nugetVersion = "3.5.0-beta2",
  [switch] $help,
  [switch] $official,
  [switch] $skipBuild,
  [switch] $skipDeploy,
  [switch] $skipRestore,
  [switch] $skipInstallRoslyn,
  [switch] $skipTest,
  [switch] $skipTest32,
  [switch] $skipTest64,
  [switch] $skipTestCore,
  [switch] $integration,
  [string] $target = "Build",
  [string] $testFilter = "*.UnitTests.dll",
  [string] $integrationTestFilter = "*.IntegrationTests.dll",
  [string] $xUnitVersion = "2.1.0"
)

function Create-Directory([string[]] $path) {
  if (!(Test-Path -path $path)) {
    New-Item -path $path -force -itemType "Directory" | Out-Null
  }
}

function Download-File([string] $address, [string] $fileName) {
  $webClient = New-Object -typeName "System.Net.WebClient"
  $webClient.DownloadFile($address, $fileName)
}

function Get-ProductVersion([string[]] $path) {
  if (!(Test-Path -path $path)) {
    return ""
  }

  $item = Get-Item -path $path
  return $item.VersionInfo.ProductVersion
}

function Get-RegistryValue([string] $keyName, [string] $valueName) {
  $registryKey = Get-ItemProperty -path $keyName
  return $registryKey.$valueName
}

function Locate-ArtifactsPath {
  $rootPath = Locate-RootPath
  $artifactsPath = Join-Path -path $rootPath -ChildPath "artifacts\"

  Create-Directory -path $artifactsPath
  return Resolve-Path -path $artifactsPath
}

function Locate-MSBuild {
  $msbuildPath = Locate-MSBuildPath
  $msbuild = Join-Path -path $msbuildPath -childPath "MSBuild.exe"

  if (!(Test-Path -path $msbuild)) {
    throw "The specified MSBuild version ($msbuildVersion) could not be located."
  }

  return Resolve-Path -path $msbuild
}

function Locate-MSBuildLogPath {
  $artifactsPath = Locate-ArtifactsPath
  $msbuildLogPath = Join-Path -path $artifactsPath -ChildPath "$configuration\log\"

  Create-Directory -path $msbuildLogPath
  return Resolve-Path -path $msbuildLogPath
}

function Locate-MSBuildPath {
  $msbuildVersionPath = Locate-MSBuildVersionPath
  $msbuildPath = Get-RegistryValue -keyName $msbuildVersionPath -valueName "MSBuildToolsPath"
  return Resolve-Path -path $msbuildPath
}

function Locate-MSBuildVersionPath {
  $msbuildVersionPath = "HKLM:\SOFTWARE\WOW6432Node\Microsoft\MSBuild\ToolsVersions\$msbuildVersion"

  if (!(Test-Path -path $msbuildVersionPath)) {
    $msbuildVersionPath = "HKLM:\SOFTWARE\Microsoft\MSBuild\ToolsVersions\$msbuildVersion"

    if (!(Test-Path -path $msbuildVersionPath)) {
      throw "The specified MSBuild version ($msbuildVersion) could not be located."
    }
  }

  return Resolve-Path -path $msbuildVersionPath
}

function Locate-NuGet {
  $rootPath = Locate-RootPath
  $nuget = Join-Path -path $rootPath -childPath "nuget.exe"

  if (Test-Path -path $nuget) {
    $currentVersion = Get-ProductVersion -path $nuget

    if ($currentVersion.StartsWith($nugetVersion)) {
      return Resolve-Path -path $nuget
    }

    Write-Host -object "The located version of NuGet ($currentVersion) is out of date. The specified version ($nugetVersion) will be downloaded instead."
    Remove-Item -path $nuget | Out-Null
  }

  Download-File -address "https://dist.nuget.org/win-x86-commandline/v$nugetVersion/NuGet.exe" -fileName $nuget

  if (!(Test-Path -path $nuget)) {
    throw "The specified NuGet version ($nugetVersion) could not be downloaded."
  }

  return Resolve-Path -path $nuget
}

function Locate-NuGetConfig {
  $rootPath = Locate-RootPath
  $nugetConfig = Join-Path -path $rootPath -childPath "nuget.config"
  return Resolve-Path -path $nugetConfig
}

function Locate-PackagesPath {
  if ($env:NUGET_PACKAGES -eq $null) {
    $env:NUGET_PACKAGES =  Join-Path -path $env:UserProfile -childPath ".nuget\packages\"
  }

  $packagesPath = $env:NUGET_PACKAGES

  Create-Directory -path $packagesPath
  return Resolve-Path -path $packagesPath
}

function Locate-RootPath {
  $scriptPath = Locate-ScriptPath
  $rootPath = Join-Path -path $scriptPath -childPath "..\..\..\"
  return Resolve-Path -path $rootPath
}

function Locate-ScriptPath {
  $myInvocation = Get-Variable -name "MyInvocation" -scope "Script"
  $scriptPath = Split-Path -path $myInvocation.Value.MyCommand.Definition -parent
  return Resolve-Path -path $scriptPath
}

function Locate-Solution {
  $rootPath = Locate-RootPath
  $solution = Join-Path -path $rootPath -childPath "SymStore.sln"
  return Resolve-Path -path $solution
}

function Locate-Toolset {
  $rootPath = Locate-RootPath
  $toolset = Join-Path -path $rootPath -childPath "build\Toolset\project.json"
  return Resolve-Path -path $toolset
}

function Locate-xUnit-x86 {
  $xUnitPath = Locate-xUnitPath
  $xUnit = Join-Path -path $xUnitPath -childPath "xunit.console.x86.exe"

  if (!(Test-Path -path $xUnit)) {
    throw "The specified xUnit version ($xUnitVersion) could not be located."
  }

  return Resolve-Path -path $xUnit
}

function Locate-xUnit-x64 {
  $xUnitPath = Locate-xUnitPath
  $xUnit = Join-Path -path $xUnitPath -childPath "xunit.console.exe"

  if (!(Test-Path -path $xUnit)) {
    throw "The specified xUnit version ($xUnitVersion) could not be located."
  }

  return Resolve-Path -path $xUnit
}

function Locate-xUnitPath {
  $packagesPath = Locate-PackagesPath
  $xUnitPath = Join-Path -path $packagesPath -childPath "xunit.runner.console\$xUnitVersion\tools\"

  Create-Directory -path $xUnitPath
  return Resolve-Path -path $xUnitPath
}

function Locate-xUnitLogPath {
  $artifactsPath = Locate-ArtifactsPath
  $xUnitLogPath = Join-Path -path $artifactsPath -ChildPath "$configuration\log\"

  Create-Directory -path $xUnitLogPath
  return Resolve-Path -path $xUnitLogPath
}

function Locate-xUnitTestBinaries {
  $artifactsPath = Locate-ArtifactsPath

  $binariesPath = Join-Path -path $artifactsPath -childPath "$configuration\bin\DesktopTests"
  $testBinaries = Get-ChildItem -path $binariesPath -filter $testFilter -recurse -force

  $xUnitTestBinaries = @()

  foreach ($xUnitTestBinary in $testBinaries) {
    $xUnitTestBinaries += $xUnitTestBinary.FullName
  }

  return $xUnitTestBinaries
}

function Locate-VsixDeployExe {
  $artifactsPath = Locate-ArtifactsPath
  
  $binariesPath = Join-Path -path $artifactsPath -childPath "$configuration\bin\DesktopTests"

  return Join-Path $binariesPath "DeployIntegrationTestVsixes\DeployIntegrationTestVsixes.exe"
}

function Locate-xUnitIntegrationTestBinaries {
  $artifactsPath = Locate-ArtifactsPath
  
  $binariesPath = Join-Path -path $artifactsPath -childPath "$configuration\bin\"
  $testBinaries = Get-ChildItem -path $binariesPath -filter $integrationTestFilter -recurse -force

  $xUnitTestBinaries = @()

  foreach ($xUnitTestBinary in $testBinaries) {
    $xUnitTestBinaries += $xUnitTestBinary.FullName
  }

  return $xUnitTestBinaries
}

function Perform-Build {
  Write-Host -object ""

  if ($skipBuild) {
    Write-Host -object "Skipping build..."
    return
  }

  $artifactsPath = Locate-ArtifactsPath
  $msbuild = Locate-MSBuild
  $msbuildLogPath = Locate-MSBuildLogPath
  $solution = Locate-Solution

  $msbuildSummaryLog = Join-Path -path $msbuildLogPath -childPath "MSBuild.log"
  $msbuildWarningLog = Join-Path -path $msbuildLogPath -childPath "MSBuild.wrn"
  $msbuildFailureLog = Join-Path -path $msbuildLogPath -childPath "MSBuild.err"

  $deploy = (-not $skipDeploy)

  Write-Host -object "Starting build..."
  & $msbuild /t:$target /p:Configuration=$configuration /p:DeployExtension=$deploy /p:DeployHive=$deployHive /p:OfficialBuild=$official /m /tv:$msbuildVersion /v:m /flp1:Summary`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$msbuildSummaryLog /flp2:WarningsOnly`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$msbuildWarningLog /flp3:ErrorsOnly`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$msbuildFailureLog /nr:false $solution

  if ($lastExitCode -ne 0) {
    throw "The build failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "The build completed successfully." -foregroundColor Green
}

function Perform-Restore {
  Write-Host -object ""

  if ($skipRestore) {
    Write-Host -object "Skipping restore..."
    return
  }

  $nuget = Locate-NuGet
  $nugetConfig = Locate-NuGetConfig
  $packagesPath = Locate-PackagesPath
  $toolset = Locate-Toolset
  $solution = Locate-Solution

  Write-Host -object "Starting restore..."
  & $nuget restore -packagesDirectory $packagesPath -msbuildVersion $msbuildVersion -verbosity quiet -nonInteractive -configFile $nugetConfig $toolset
  & $nuget restore -packagesDirectory $packagesPath -msbuildVersion $msbuildVersion -verbosity quiet -nonInteractive -configFile $nugetConfig $solution

  if ($lastExitCode -ne 0) {
    throw "The restore failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "The restore completed successfully." -foregroundColor Green
}

function Perform-Test-x86 {
  Write-Host -object ""

  if ($skipTest -or $skipTest32) {
    Write-Host -object "Skipping test x86..."
    return
  }

  $xUnit = Locate-xUnit-x86
  $xUnitLogPath = Locate-xUnitLogPath
  $xUnitTestBinaries = @(Locate-xUnitTestBinaries)
  Write-Host $xUnitTestBinaries

  $xUnitResultLog = Join-Path -path $xUnitLogPath -childPath "xUnit-x86.xml"

  Write-Host "$xUnit $xUnitTestBinaries -xml $xUnitResultLog"
  & $xUnit @xUnitTestBinaries -xml $xUnitResultLog

  if ($lastExitCode -ne 0) {
    throw "The test failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "The test completed successfully." -foregroundColor Green
}

function Perform-Test-x64 {
  Write-Host -object ""

  if ($skipTest -or $skipTest64) {
    Write-Host -object "Skipping test x64..."
    return
  }

  $xUnit = Locate-xUnit-x64
  $xUnitLogPath = Locate-xUnitLogPath
  $xUnitTestBinaries = @(Locate-xUnitTestBinaries)

  $xUnitResultLog = Join-Path -path $xUnitLogPath -childPath "xUnit-x64.xml"

  Write-Host "$xUnit $xUnitTestBinaries -xml $xUnitResultLog"
  & $xUnit @xUnitTestBinaries -xml $xUnitResultLog

  if ($lastExitCode -ne 0) {
    throw "The test failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "The test completed successfully." -foregroundColor Green
}

function Perform-Test-Core {
  Write-Host -object ""

  if ($skipTest -or $skipTestCore) {
    Write-Host -object "Skipping test Core..."
    return
  }

  $artifactsPath = Locate-ArtifactsPath
  $binariesPath = Join-Path $artifactsPath "$configuration\bin\CoreTests"
 
  $corerun = Join-Path $binariesPath "CoreRun.exe"
  $xUnit = Join-Path $binariesPath "xunit.console.netcore.exe"
  $xUnitLogPath = Locate-xUnitLogPath
  $xUnitTestBinaries = @(Locate-xUnitTestBinaries)
  Write-Host $xUnitTestBinaries

  $xUnitResultLog = Join-Path -path $xUnitLogPath -childPath "xUnit-Core.xml"

  Write-Host "$corerun $xUnit $xUnitTestBinaries -xml $xUnitResultLog"
  & $corerun $xUnit @xUnitTestBinaries -xml $xUnitResultLog

  if ($lastExitCode -ne 0) {
    throw "The test failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "The test completed successfully." -foregroundColor Green
}

function Perform-Install-Roslyn-Vsixes {
  Write-Host -object ""

  if ($skipInstallRoslyn -or $skipTest -or (-not $integration)) {
    Write-Host -object "Skipping installation of integration test vsixes..."
    return
  }
  
  Write-Host "Starting to install vsixes..."
  
  $vsixDeployExe = Locate-VsixDeployExe
  
  & $vsixDeployExe
  
  Write-Host -object "Installed integration test vsixes successfully." -foregroundColor Green
}

function Perform-Test-Integration {
  Write-Host -object ""

  if ($skipTest -or (-not $integration)) {
    Write-Host -object "Skipping integration tests..."
    return
  }
  
  $xUnit = Locate-xUnit-x64
  $xUnitLogPath = Locate-xUnitLogPath
  $xUnitTestBinaries = @(Locate-xUnitIntegrationTestBinaries)

  $xUnitResultLog = Join-Path -path $xUnitLogPath -childPath "xUnit-integration.xml"

  Write-Host -object "Starting integration tests..."
  & $xUnit @xUnitTestBinaries -xml $xUnitResultLog -noshadow

  if ($lastExitCode -ne 0) {
    throw "The test failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "The test completed successfully." -foregroundColor Green
}

function Print-Help {
  if (-not $help) {
    return
  }
  
  Write-Host -object "TestImpact Build Script"
  Write-Host -object "    Help                  - [Switch] - Prints this help message."
  Write-Host -object ""
  Write-Host -object "    Configuration         - [String] - Specifies the build configuration. Defaults to 'Debug'."
  Write-Host -object "    DeployHive            - [String] - Specifies the VSIX deployment hive. Defaults to 'TestImpact'."
  Write-Host -object "    MSBuildVersion        - [String] - Specifies the MSBuild version. Defaults to '14.0'."
  Write-Host -object "    NuGetVersion          - [String] - Specifies the NuGet version. Defaults to '3.5.0-beta'."
  Write-Host -object "    Target                - [String] - Specifies the build target. Defaults to 'Build'."
  Write-Host -object "    TestFilter            - [String] - Specifies the test filter. Defaults to '*.UnitTests.dll'."
  Write-Host -object "    IntegrationTestFilter - [String] - Specifies the integration test filter. Defaults to '*.IntegrationTests.dll'"
  Write-Host -object "    xUnitVersion          - [String] - Specifies the xUnit version. Defaults to '2.1.0'."
  Write-Host -object ""
  Write-Host -object "    Official              - [Switch] - Indicates this is an official build which changes the semantic version."
  Write-Host -object "    SkipBuild             - [Switch] - Indicates the build step should be skipped."
  Write-Host -object "    SkipDeploy            - [Switch] - Indicates the VSIX deployment step should be skipped."
  Write-Host -object "    SkipInstallRoslyn     - [Switch] - Indicates the installation of Roslyn VSIX step should be skipped."
  Write-Host -object "    SkipRestore           - [Switch] - Indicates the restore step should be skipped."
  Write-Host -object "    SkipTest              - [Switch] - Indicates the test step should be skipped."
  Write-Host -object "    SkipTest32            - [Switch] - Indicates the 32-bit Unit Tests should be skipped."
  Write-Host -object "    SkipTest64            - [Switch] - Indicates the 64-bit Unit Tests should be skipped."
  Write-Host -object "    SkipTestCore          - [Switch] - Indicates the Core CLR Unit Tests should be skipped."
  Write-Host -object "    Integration           - [Switch] - Indicates the Integration Tests should be run."
  
  Exit 0
}

# Enforce deployment when running integration tests.
# This ensures that installed extension's timestamp is the same as in the artifacts folder.
if ((-not $skipTest) -and $integration) {
  $skipBuild = $false
  $skipDeploy = $false
}

Print-Help
Perform-Restore
Perform-Build
Perform-Install-Roslyn-Vsixes
Perform-Test-x86
Perform-Test-x64
Perform-Test-Core
Perform-Test-Integration
