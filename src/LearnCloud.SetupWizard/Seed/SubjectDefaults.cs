namespace LearnCloud.SetupWizard.Seed;

public static class SubjectDefaults
{
    // Starter lists the school can accept or edit
    public static List<StarterSubject> PrimaryStarter => new()
    {
        new("Mathematics", "MATH", true),
        new("English", "ENG", true),
        new("Agriculture", "AGR", true),
        new("Science and Technology", "SCI", true),
        new("Heritage and Social Sciences", "HER", true),
        new("Physical Education and Arts", "PEA", true),
        new("Family and Heritage", "FAM", false),
        new("ICT", "ICT", false),
    };

    public static List<StarterSubject> SecondaryStarter => new()
    {
        new("Mathematics", "MATH", true),
        new("English Language", "ENG", true),
        new("Combined Science", "CSC", true),
        new("History", "HIST", true),
        new("Geography", "GEO", true),
        new("Agriculture", "AGR", false),
        new("Computer Science", "COMPSCI", false),
        new("Commerce", "COMM", false),
        new("Accounting", "ACC", false),
        new("Biology", "BIO", false),
        new("Chemistry", "CHEM", false),
        new("Physics", "PHY", false),
        new("Shona", "SHONA", false),
        new("Ndebele", "NDEB", false),
        new("Heritage Studies", "HER", false),
        new("Physical Education", "PE", false),
        new("Art and Design", "ART", false),
        new("Literature in English", "LIT", false),
    };

    public static List<StarterSubject> CombinedStarter => PrimaryStarter.Union(SecondaryStarter).GroupBy(s=>s.Code).Select(g=>g.First()).ToList();

    public static List<StarterSubject> GetBySchoolType(string schoolType) => schoolType.ToLower() switch
    {
        "primary" => PrimaryStarter,
        "secondary" => SecondaryStarter,
        _ => CombinedStarter
    };
}

public record StarterSubject(string Name, string Code, bool IsCore);

public static class GradingScaleDefaults
{
    // Configurable bands with symbol, description and range - sensible default Zimbabwe ZIMSEC-like
    public static List<GradingBand> ZimsecPrimary => new()
    {
        new("A", "Excellent", 80, 100, 5, "#2E7D32"),
        new("B", "Very Good", 70, 79, 4, "#5A94C1"),
        new("C", "Good", 60, 69, 3, "#307EC0"),
        new("D", "Fair", 50, 59, 2, "#B7791F"),
        new("E", "Pass", 40, 49, 1, "#844CAD"),
        new("U", "Fail - Needs Improvement", 0, 39, 0, "#C62828"),
    };

    public static List<GradingBand> ZimsecSecondary => new()
    {
        new("A", "Excellent - Distinction", 80, 100, 5, "#2E7D32"),
        new("B", "Very Good", 70, 79, 4, "#5A94C1"),
        new("C", "Good - Credit", 60, 69, 3, "#307EC0"),
        new("D", "Fair", 50, 59, 2, "#B7791F"),
        new("E", "Pass", 40, 49, 1, "#844CAD"),
        new("U", "Ungraded - Fail", 0, 39, 0, "#C62828"),
    };

    public static List<GradingBand> CompetencyBased => new()
    {
        new("EX", "Exceeding Expectation", 80, 100, 4, "#2E7D32"),
        new("ME", "Meeting Expectation", 60, 79, 3, "#307EC0"),
        new("AE", "Approaching Expectation", 40, 59, 2, "#B7791F"),
        new("BE", "Below Expectation", 0, 39, 1, "#C62828"),
    };

    public static List<GradingBand> GetDefault(string? preference = null) => preference?.ToLower() switch
    {
        "primary" => ZimsecPrimary,
        "competency" => CompetencyBased,
        _ => ZimsecSecondary
    };
}

public record GradingBand(string Symbol, string Description, decimal MinScore, decimal MaxScore, decimal? GradePoint, string Color);

// Departments and Staff Roles defaults
public static class DepartmentDefaults
{
    public static List<string> DefaultDepartments => new()
    {
        "Administration",
        "Sciences",
        "Commercials",
        "Arts and Languages",
        "Mathematics",
        "Practical Subjects",
        "Sports and Clubs",
        "Guidance and Counseling"
    };

    public static List<string> DefaultCustomRoles => new()
    {
        "HOD",
        "Senior Teacher",
        "Lab Technician",
        "Librarian",
        "Sports Coach",
        "Matron/Patron"
    };
}

// Academic Year and Terms defaults for Zimbabwe
public static class AcademicDefaults
{
    public static (DateTime start, DateTime end) GetCurrentAcademicYear()
    {
        var year = DateTime.UtcNow.Year;
        // If we are in Oct-Dec, academic year is next year? For ZW, academic year Jan-Dec
        if (DateTime.UtcNow.Month >= 10) year = DateTime.UtcNow.Year + 1;
        return (new DateTime(year, 1, 10), new DateTime(year, 12, 5));
    }

    public static List<(string name, int number, DateTime start, DateTime end)> GetThreeTerms(DateTime yearStart, DateTime yearEnd)
    {
        // Sensible defaults: Term1 Jan-Apr, Term2 May-Aug, Term3 Sep-Dec
        var year = yearStart.Year;
        return new()
        {
            ("Term 1", 1, new DateTime(year, 1, 10), new DateTime(year, 4, 15)),
            ("Term 2", 2, new DateTime(year, 5, 10), new DateTime(year, 8, 10)),
            ("Term 3", 3, new DateTime(year, 9, 10), new DateTime(year, 12, 5)),
        };
    }

    public static List<(string name, int number, DateTime start, DateTime end)> GetTwoTerms(DateTime yearStart, DateTime yearEnd)
    {
        var year = yearStart.Year;
        var mid = yearStart.AddDays((yearEnd - yearStart).TotalDays / 2);
        return new()
        {
            ("Semester 1", 1, yearStart, mid.AddDays(-1)),
            ("Semester 2", 2, mid, yearEnd),
        };
    }
}
