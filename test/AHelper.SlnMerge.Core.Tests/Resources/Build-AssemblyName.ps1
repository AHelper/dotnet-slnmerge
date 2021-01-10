Import-Module ./Common.psm1
$ErrorActionPreference = "Stop"

$directoryBuildTargets = Get-DirectoryBuildTargets
$nugetConfig = Get-NugetConfig

Remove-Item -Recurse AssemblyName -ErrorAction Ignore

if (Test-Path AssemblyName.zip) {
    Expand-Archive AssemblyName.zip -DestinationPath .
} else {
    mkdir AssemblyName >$null
    Set-Location AssemblyName
    New-Projects -SolutionName A -Names A
    New-Projects -SolutionName B -Names B
    Add-Property -CsprojPath B/B/B.csproj -Property "<AssemblyName>B.AssemblyName</AssemblyName>"
    Set-Content nuget.config $nugetConfig
    Set-Content Directory.Build.targets $directoryBuildTargets
    dotnet pack A
    dotnet pack B
    dotnet add A/A package B.AssemblyName
    dotnet build A
    dotnet build-server shutdown
    Set-Location ..
    Compress-Archive AssemblyName AssemblyName.zip
}