Feature: Advanced merging
    Catch-all for testing features

    Scenario: AspNetTest
        Given test project created with "AdvancedMerging/AspNetTest.xml"
        When merging solutions: AspNet, Library
        Then project AspNet/AspNet/AspNet.csproj should reference ../../Library/Library.csproj
        And solution AspNet should include ../Library/Library.csproj

    Scenario: Chained
        Given test project created with "AdvancedMerging/Chained.xml"
        When merging solutions: A, B, C, D
        Then project A/AA/AA.csproj should reference ../../B/BA/BA.csproj
        And project A/AB/AB.csproj should reference ../../C/CB/CB.csproj
        And project B/BB/BB.csproj should reference ../../C/CA/CA.csproj
        And project C/CB/CB.csproj should reference ../../D/DA/DA.csproj
        And solution A should include ../B/BA/BA.csproj, ../B/BB/BB.csproj, ../C/CA/CA.csproj, ../C/CB/CB.csproj, ../D/DA/DA.csproj, ../D/DB/DB.csproj
        And solution B should include ../C/CA/CA.csproj, ../C/CB/CB.csproj, ../D/DA/DA.csproj, ../D/DB/DB.csproj
        And solution C should include ../D/DA/DA.csproj, ../D/DB/DB.csproj

    Scenario: Multiple Frameworks
        Given test project created with "AdvancedMerging/MultiFramework.xml"
        When merging solutions: A, B, C, D
        Then project A/A/A.csproj should reference ../../B/BA/BA.csproj for framework netstandard2.0
        Then project A/A/A.csproj should reference ../../B/BB/BB.csproj for framework netstandard2.0
        And project A/A/A.csproj should reference ../../C/CA/CA.csproj for framework net5.0
        And project A/A/A.csproj should reference ../../C/CB/CB.csproj for framework net5.0
        And project A/A/A.csproj should reference ../../D/DA/DA.csproj
        And project A/A/A.csproj should reference ../../D/DB/DB.csproj
        And project B/BA/BA.csproj should reference ../../D/DA/DA.csproj
        And project B/BA/BA.csproj should reference ../../D/DB/DB.csproj
        And project C/CA/CA.csproj should reference ../../D/DA/DA.csproj
        And project C/CA/CA.csproj should reference ../../D/DB/DB.csproj
        And project A/A/A.csproj should have 6 item groups
        And project B/BA/BA.csproj should have 2 item groups
        And project C/CA/CA.csproj should have 2 item groups
        And solution A should include ../B/BA/BA.csproj, ../B/BB/BB.csproj, ../C/CA/CA.csproj, ../C/CB/CB.csproj
        And solution B should include ../D/DA/DA.csproj, ../D/DB/DB.csproj
        And solution C should include ../D/DA/DA.csproj, ../D/DB/DB.csproj

    Scenario: Merge chain in multiple steps
        Given test project created with "AdvancedMerging/MergeChain.xml"
        When merging solutions: A, B
        And merging solutions: B, C
        And merging solutions with exceptions: A, B
        Then solution A/A.sln should include ../B/BA/BA.csproj, ../B/BB/BB.csproj, ../C/C/C.csproj
        And it should print warning FileReadException for 'C/C/C.csproj'

    Scenario: Transitive
        Given test project created with "AdvancedMerging/Transitive.xml"
        And nugets created for solution "A/A.sln" with version "1.1.0"
        And nugets created for solution "C/C.sln" with version "1.1.0"
        When merging solutions with restoring: A, B
        Then project B/BA/BA.csproj should reference ../../A/AA/AA.csproj,../../A/AB/AB.csproj
        And project B/BB/BB.csproj should reference ../../A/AB/AB.csproj
        And solution B/B.sln should include ../A/AA/AA.csproj, ../A/AB/AB.csproj
        
    # Addressing #18
    Scenario: Transitive2
        Given test project created with "AdvancedMerging/Transitive2.xml"
        And nugets created for solution "B/B.sln" with version "1.1.0"
        When merging solutions with restoring: A, B
        Then project A/AA/AA.csproj should reference ../../B/BA/BA.csproj
        Then project A/AA/AA.csproj should not reference ../AD/AD.csproj
        And project A/AC/AC.csproj should not reference ../../B/BA/BA.csproj

    Scenario: Versions
        Given test project created with "AdvancedMerging/Versions.xml"
        And nugets created for solution "Versions.C/Versions.C.sln" with version "2.2.2"
        And nugets created for solution "Versions.B/Versions.B.sln" with version "2.2.2"
        When merging solutions with restoring: Versions.A, Versions.C
        Then project Versions.A/Versions.AA/Versions.AA.csproj should reference ../../Versions.C/Versions.CA/Versions.CA.csproj
        And project Versions.C/Versions.CA/Versions.CA.csproj should have high version

    Scenario: VersionsWithExisting
        Given test project created with "AdvancedMerging/VersionsWithExisting.xml"
        And nugets created for solution "VersionsWithExisting.C/VersionsWithExisting.C.sln" with version "2.2.2"
        And nugets created for solution "VersionsWithExisting.B/VersionsWithExisting.B.sln" with version "2.2.2"
        When merging solutions with restoring: VersionsWithExisting.A, VersionsWithExisting.C
        Then project VersionsWithExisting.A/VersionsWithExisting.AA/VersionsWithExisting.AA.csproj should reference ../../VersionsWithExisting.C/VersionsWithExisting.CA/VersionsWithExisting.CA.csproj
        And project VersionsWithExisting.C/VersionsWithExisting.CA/VersionsWithExisting.CA.csproj should have high version with original version 2.2.2

    Scenario: UndoingVersionsWithExisting
        Given test project created with "AdvancedMerging/VersionsWithExisting.xml"
        And nugets created for solution "VersionsWithExisting.C/VersionsWithExisting.C.sln" with version "2.2.2"
        And nugets created for solution "VersionsWithExisting.B/VersionsWithExisting.B.sln" with version "2.2.2"
        When merging solutions with restoring: VersionsWithExisting.A, VersionsWithExisting.C
        And undoing merges in solutions: VersionsWithExisting.A, VersionsWithExisting.C
        Then project VersionsWithExisting.A/VersionsWithExisting.AA/VersionsWithExisting.AA.csproj should not reference ../../VersionsWithExisting.C/VersionsWithExisting.CA/VersionsWithExisting.CA.csproj
        And project VersionsWithExisting.C/VersionsWithExisting.CA/VersionsWithExisting.CA.csproj should not have high version with original version 2.2.2
