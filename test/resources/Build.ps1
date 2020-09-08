Param([string] $DirName)

function New-Projects([string] $SolutionName, [string[]] $Names)
{
    mkdir $SolutionName >$null
    cd $SolutionName
    mkdir $Names >$null

    foreach ($name in $Names)
    {
        cd $name
        dotnet new classlib -f netstandard2.0
        cd ..
    }
    dotnet new sln
    dotnet sln add @Names
    cd ..
}

function Add-Property([string] $CsprojPath, [xml] $Property)
{
    $xml = [xml](Get-Content -Raw $CsprojPath)
    $xml.Project.PropertyGroup.AppendChild($xml.ImportNode($Property.DocumentElement, $true)) >$null
    $xml.Save((Join-Path (pwd) $CsprojPath))
}

$beNuspec = @'
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
    <metadata>
        <!-- Required elements-->
        <id>BE.Nuspec</id>
        <version>1.0.0</version>
        <description>description</description>
        <authors>authors</authors>
        <dependencies>
            <group targetFramework=".NETStandard2.0" />
        </dependencies>
    </metadata>
    <files>
        <file src="bin\Debug\netstandard2.0\BE.AssemblyName.dll" target="lib\netstandard2.0" />
    </files>
</package>
'@
$bfDirectoryBuildTargets = @'
<Project>
    <PropertyGroup>
        <PackageId>BF.Directory.Build.targets</PackageId>
        <PackageOutputPath>$(MSBuildThisFileDirectory)../../feed</PackageOutputPath>
    </PropertyGroup>
</Project>
'@
$directoryBuildTargets = @'
<Project>
    <PropertyGroup>
        <PackageOutputPath>$(MSBuildThisFileDirectory)feed</PackageOutputPath>
    </PropertyGroup>
</Project>
'@
$nugetConfig = @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <!--To inherit the global NuGet package sources remove the <clear/> line below -->
    <clear />
    <add key="feed" value="./feed/" />
  </packageSources>
</configuration>
'@

mkdir $DirName >$null
cd $DirName
New-Projects -SolutionName A -Names AA, AB
New-Projects -SolutionName B -Names BA, BB, BC, BD, BE, BF, BG
New-Projects -SolutionName C -Names CA, CB, CC, CD
Add-Property -CsprojPath B\BC\BC.csproj -Property "<AssemblyName>BC.AssemblyName</AssemblyName>"
Add-Property -CsprojPath B\BD\BD.csproj -Property "<AssemblyName>BD.AssemblyName</AssemblyName>"
Add-Property -CsprojPath B\BD\BD.csproj -Property "<PackageId>BD.PackageId</PackageId>"
Add-Property -CsprojPath B\BE\BE.csproj -Property "<AssemblyName>BE.AssemblyName</AssemblyName>"
Add-Property -CsprojPath B\BE\BE.csproj -Property "<PackageId>BE.PackageId</PackageId>"
Add-Property -CsprojPath B\BE\BE.csproj -Property "<NuspecFile>BE.nuspec</NuspecFile>"
Add-Property -CsprojPath B\BF\BF.csproj -Property "<AssemblyName>BF.AssemblyName</AssemblyName>"
Add-Property -CsprojPath B\BF\BF.csproj -Property "<PackageId>BF.PackageId</PackageId>"
Set-Content B\BE\BE.nuspec $beNuspec
Set-Content B\BF\Directory.Build.targets $bfDirectoryBuildTargets
Set-Content Directory.Build.targets $directoryBuildTargets
Set-Content nuget.config $nugetConfig
dotnet pack A
dotnet pack B
dotnet pack C
dotnet add A\AA reference A\AB
dotnet add A\AA package BA
dotnet add A\AA package BC.AssemblyName
dotnet add A\AA package BD.PackageId
dotnet add A\AA package BE.Nuspec
dotnet add A\AA package BF.Directory.Build.targets
dotnet add A\AB package CA
dotnet add B\BA reference B\BB
dotnet add B\BB package CB
dotnet add B\BG package CD
dotnet add C\CA reference C\CC
dotnet add C\CB reference C\CC
dotnet add C\CD package BG
dotnet build A
dotnet build B
dotnet build C
cd ..