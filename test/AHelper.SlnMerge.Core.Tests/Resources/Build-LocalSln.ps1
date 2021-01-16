Import-Module ./Common.psm1
$ErrorActionPreference = "Stop"

$directoryBuildTargets = Get-DirectoryBuildTargets
$nugetConfig = Get-NugetConfig

Remove-Item -Recurse LocalSln -ErrorAction Ignore

if (Test-Path LocalSln.zip) {
    Expand-Archive LocalSln.zip -DestinationPath .
} else {
    mkdir LocalSln >$null
    Set-Location LocalSln
    New-Projects -SolutionName A -Names A
    dotnet new sln
    dotnet new classlib
    dotnet sln add .
    Set-Content nuget.config $nugetConfig
    Set-Content Directory.Build.targets $directoryBuildTargets
    dotnet pack A
    dotnet pack .
    dotnet add package A
    dotnet build .
    dotnet build-server shutdown
    Set-Location ..
    Compress-Archive LocalSln LocalSln.zip
}