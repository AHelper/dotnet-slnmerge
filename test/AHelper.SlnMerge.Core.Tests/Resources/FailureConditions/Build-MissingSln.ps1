Import-Module ./Common.psm1
$ErrorActionPreference = "Stop"

$directoryBuildTargets = Get-DirectoryBuildTargets
$nugetConfig = Get-NugetConfig

Remove-Item -Recurse MissingSln -ErrorAction Ignore

if (Test-Path MissingSln.zip) {
    Expand-Archive MissingSln.zip -DestinationPath .
} else {
    mkdir MissingSln >$null
    Set-Location MissingSln
    New-Projects -SolutionName A -Names A
    New-Projects -SolutionName B -Names B
    Remove-Item B/B.sln
    dotnet build-server shutdown
    Set-Location ..
    Compress-Archive MissingSln MissingSln.zip
}