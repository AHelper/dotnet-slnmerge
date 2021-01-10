function New-Projects([string] $SolutionName, [string[]] $Names) {
    mkdir $SolutionName >$null
    Set-Location $SolutionName
    mkdir $Names >$null

    foreach ($name in $Names) {
        Set-Location $name
        dotnet new classlib -f netstandard2.0
        Set-Location ..
    }
    dotnet new sln
    dotnet sln add @Names
    Set-Location ..
}

function Add-Property([string] $CsprojPath, [xml] $Property) {
    $xml = [xml](Get-Content -Raw $CsprojPath)
    $xml.Project.PropertyGroup.AppendChild($xml.ImportNode($Property.DocumentElement, $true)) >$null
    $xml.Save((Join-Path (Get-Location) $CsprojPath))
}

function Get-Nuspec([string] $Id, [string] $AssemblyName) {
    return @"
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
    <metadata>
        <!-- Required elements-->
        <id>$Id</id>
        <version>1.0.0</version>
        <description>description</description>
        <authors>authors</authors>
        <dependencies>
            <group targetFramework=".NETStandard2.0" />
        </dependencies>
    </metadata>
    <files>
        <file src="bin/Debug/netstandard2.0/$AssemblyName.dll" target="lib/netstandard2.0" />
    </files>
</package>
"@
}

function Get-DirectoryBuildTargets([string] $PackageId) {
    if ($PackageId) {
        return @"
<Project>
    <PropertyGroup>
        <PackageId>$PackageId</PackageId>
        <PackageOutputPath>`$(MSBuildThisFileDirectory)../../feed</PackageOutputPath>
    </PropertyGroup>
</Project>
"@
    }
    return @'
<Project>
    <PropertyGroup>
        <PackageOutputPath>$(MSBuildThisFileDirectory)feed</PackageOutputPath>
    </PropertyGroup>
</Project>
'@
}

function Get-NugetConfig() {
    return @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <!--To inherit the global NuGet package sources remove the <clear/> line below -->
    <clear />
    <add key="feed" value="./feed/" />
  </packageSources>
</configuration>
'@
}