Feature: merging solutions: without options
	The basic usage of slnmerge is to combine 2 or more solutions such that
    ProjectReferences are added for corresponding PackageReferences when a
    local project is found in one of the solutions.  The referenced project 
    is also added to the solution.

    Scenario: AssemblyName
        Given test project "AssemblyName" created with "Build-AssemblyName.ps1"
        When merging solutions: A, B
        Then project A/A should reference ../../B/B/B.csproj
        And solution A should include ../B/B/B.csproj

    Scenario: Basic
        Given test project "Basic" created with "Build-Basic.ps1"
        When merging solutions: A, B
        Then project A/A should reference ../../B/B/B.csproj
        And solution A should include ../B/B/B.csproj

    Scenario: Chained
        Given test project "Chained" created with "Build-Chained.ps1"
        When merging solutions: A, B, C, D
        Then project A/AA should reference ../../B/BA/BA.csproj
        And project A/AB should reference ../../C/CB/CB.csproj
        And project B/BA should reference ../BB/BB.csproj
        And project B/BB should reference ../../C/CA/CA.csproj
        And project C/CA should reference ../CB/CB.csproj
        And project C/CB should reference ../../D/DA/DA.csproj
        And project D/DA should reference ../DB/DB.csproj
        And solution A should include ../B/BA/BA.csproj, ../B/BB/BB.csproj, ../C/CA/CA.csproj, ../C/CB/CB.csproj, ../D/DA/DA.csproj, ../D/DB/DB.csproj
        And solution B should include ../C/CA/CA.csproj, ../C/CB/CB.csproj, ../D/DA/DA.csproj, ../D/DB/DB.csproj
        And solution C should include ../D/DA/DA.csproj, ../D/DB/DB.csproj

    Scenario: DifferentSlnName
        Given test project "DifferentSlnName" created with "Build-DifferentSlnName.ps1"
        When merging solutions: A, B
        Then project A/A should reference ../../B/B/B.csproj
        And solution A should include ../B/B/B.csproj

    Scenario: DirectoryBuildTargets
        Given test project "DirectoryBuildTargets" created with "Build-DirectoryBuildTargets.ps1"
        When merging solutions: A, B
        Then project A/A should reference ../../B/B/B.csproj
        And solution A should include ../B/B/B.csproj

    Scenario: Nuspec
        Given test project "Nuspec" created with "Build-Nuspec.ps1"
        When merging solutions: A, B
        Then project A/A should reference ../../B/B/B.csproj
        And solution A should include ../B/B/B.csproj

    Scenario: PackageId
        Given test project "PackageId" created with "Build-PackageId.ps1"
        When merging solutions: A, B
        Then project A/A should reference ../../B/B/B.csproj
        And solution A should include ../B/B/B.csproj

    Scenario: Precedence
        Given test project "Precedence" created with "Build-Precedence.ps1"
        When merging solutions: A, B
        Then project A/AA should reference ../../B/BB/BB.csproj
        And project A/AB should reference ../../B/BA/BA.csproj
        And project A/AC should reference ../../B/BC/BC.csproj
        And project A/AD should reference ../../B/BD/BD.csproj
        And solution A should include ../B/BA/BA.csproj, ../B/BB/BB.csproj, ../B/BC/BC.csproj, ../B/BD/BD.csproj

    Scenario: LocalSln
        Given test project "LocalSln" created with "Build-LocalSln.ps1"
        When merging the local solution with: A
        Then project . should reference A/A/A.csproj

    Scenario: Multiple Frameworks
        Given test project "MultiFramework" created with "Build-MultiFramework.ps1"
        When merging solutions: A, B, C
        Then project A/A/A.csproj should reference ../../B/B/B.csproj for framework netstandard2.0
        And project A/A/A.csproj should reference ../../C/C/C.csproj for framework net5.0
        And solution A should include ../B/B/B.csproj, ../C/C/C.csproj

