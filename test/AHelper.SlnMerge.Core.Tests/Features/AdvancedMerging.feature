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

