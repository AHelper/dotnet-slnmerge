Import-Module ./Common.psm1
$ErrorActionPreference = "Stop"

$directoryBuildTargets = Get-DirectoryBuildTargets
$nugetConfig = Get-NugetConfig

Remove-Item -Recurse SlnFolder -ErrorAction Ignore

if (Test-Path SlnFolder.zip) {
    Expand-Archive SlnFolder.zip -DestinationPath .
} else {
    mkdir SlnFolder >$null
    Set-Location SlnFolder
    New-Projects -SolutionName B -Names B
    Set-Content nuget.config $nugetConfig
    Set-Content Directory.Build.targets $directoryBuildTargets
    New-Item -ItemType Directory A/src/A
    cd A/src/A
    dotnet new classlib
    cd ../..
    dotnet new sln
    dotnet sln add src/A
    cd ..
    dotnet pack A
    dotnet pack B
    dotnet add A/src/A package B
    dotnet build A
    dotnet build-server shutdown
    Set-Location ..
    Compress-Archive SlnFolder SlnFolder.zip
}