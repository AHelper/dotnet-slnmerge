Import-Module ./Common.psm1
$ErrorActionPreference = "Stop"

$directoryBuildTargets = Get-DirectoryBuildTargets
$nugetConfig = Get-NugetConfig

Remove-Item -Recurse AmbiguousSolution -ErrorAction Ignore

if (Test-Path AmbiguousSolution.zip) {
    Expand-Archive AmbiguousSolution.zip -DestinationPath .
} else {
    mkdir AmbiguousSolution >$null
    Set-Location AmbiguousSolution
    New-Projects -SolutionName A -Names A
    dotnet new sln -o A -n B
    Set-Content nuget.config $nugetConfig
    Set-Content Directory.Build.targets $directoryBuildTargets
    dotnet build-server shutdown
    Set-Location ..
    Compress-Archive AmbiguousSolution AmbiguousSolution.zip
}