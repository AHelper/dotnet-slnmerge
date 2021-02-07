Feature: PackageId identification
    There are many properties and files that are used to determine the PackageId.  Slnmerge should
    match MSBuild's behavior in calculating the PackageId.

    Scenario: AssemblyName
        Given test project created with "PackageIdIdentification/AssemblyName.xml"
        When merging solutions: A, B
        Then project A/A/A.csproj should reference ../../B/B/B.csproj
        And solution A should include ../B/B/B.csproj

    Scenario: Basic
        Given test project created with "PackageIdIdentification/Basic.xml"
        When merging solutions: A, B
        Then project A/A/A.csproj should reference ../../B/B/B.csproj
        And solution A should include ../B/B/B.csproj

    Scenario: DirectoryBuildTargets
        Given test project created with "PackageIdIdentification/DirectoryBuildTargets.xml"
        When merging solutions: A, B
        Then project A/A/A.csproj should reference ../../B/B/B.csproj
        And solution A should include ../B/B/B.csproj

    Scenario: Nuspec
        Given test project created with "PackageIdIdentification/Nuspec.xml"
        When merging solutions: A, B
        Then project A/A/A.csproj should reference ../../B/B/B.csproj
        And solution A should include ../B/B/B.csproj

    Scenario: PackageId
        Given test project created with "PackageIdIdentification/PackageId.xml"
        When merging solutions: A, B
        Then project A/A/A.csproj should reference ../../B/B/B.csproj
        And solution A should include ../B/B/B.csproj

    Scenario: Precedence
        Given test project created with "PackageIdIdentification/Precedence.xml"
        When merging solutions: A, B
        Then project A/AA/AA.csproj should reference ../../B/BB/BB.csproj
        And project A/AB/AB.csproj should reference ../../B/BA/BA.csproj
        And project A/AC/AC.csproj should reference ../../B/BC/BC.csproj
        And project A/AD/AD.csproj should reference ../../B/BD/BD.csproj
        And solution A should include ../B/BA/BA.csproj, ../B/BB/BB.csproj, ../B/BC/BC.csproj, ../B/BD/BD.csproj
