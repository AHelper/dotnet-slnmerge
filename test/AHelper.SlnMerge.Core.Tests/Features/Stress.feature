Feature: Stress testing
    Slnmerge should be able to merge many large solutions without taking an unreasonable amount of
    time.  Basic progress reporting should be done if a large project is detected to provide 
    feedback to the user.

    Scenario: Large
        Given test project created with "Stress/Large.xml"
        When merging solutions: A, B, C, D, E
        Then project A/AZ/AZ.csproj should reference ../../B/BZ/BZ.csproj
        And solution A should include ../E/EZ/EZ.csproj
