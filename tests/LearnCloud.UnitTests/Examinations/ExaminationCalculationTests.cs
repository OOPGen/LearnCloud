using LearnCloud.Examinations.Services;
using Xunit;

namespace LearnCloud.Examinations.Tests;

public class ExaminationCalculationTests
{
    private readonly ExaminationCalculationService _svc = new();

    // Grading scale support letter grades, symbol grades and percentage bands
    [Fact]
    public void Grading_Symbol_Grades_Percentage_Bands()
    {
        var scale = new List<GradingBand>
        {
            new GradingBand("A", "Excellent", 80, 100),
            new GradingBand("B", "Very Good", 70, 79.99m),
            new GradingBand("C", "Good", 60, 69.99m),
            new GradingBand("D", "Fair", 50, 59.99m),
            new GradingBand("E", "Pass", 40, 49.99m),
            new GradingBand("U", "Fail", 0, 39.99m),
        };

        Assert.Equal("A", _svc.GetGradeFromScale(85, scale));
        Assert.Equal("B", _svc.GetGradeFromScale(75, scale));
        Assert.Equal("U", _svc.GetGradeFromScale(20, scale));
        Assert.Equal("A", _svc.GetGradeFromScale(80, scale)); // boundary inclusive
        Assert.Null(_svc.GetGradeFromScale(101, scale)); // gap beyond 100
    }

    // Weighted composite results: CA vs Exam, weights configurable per subject and per level
    [Fact]
    public void Composite_CA_30_Exam_70()
    {
        var ca = new List<AssessmentScore>
        {
            new AssessmentScore(1, 60, 100, false, false), // 60%
            new AssessmentScore(2, 80, 100, false, false)  // 80% => avg 70%
        };
        var exam = new List<AssessmentScore>
        {
            new AssessmentScore(3, 70, 100, false, true) // 70%
        };

        var composite = _svc.CalculateCompositeResult(ca, exam, caWeight: 30, examWeight: 70, AbsentHandling.ExcludeExceptExamAsZero);
        // CA avg 70 *0.3=21, Exam 70*0.7=49 => 70
        Assert.Equal(70m, composite);
    }

    [Fact]
    public void Composite_Joined_MidYear_Missing_CA_ProRated()
    {
        // Learner joined mid-year, has no CA, only exam
        var ca = new List<AssessmentScore>(); // empty - joined late
        var exam = new List<AssessmentScore>
        {
            new AssessmentScore(3, 80, 100, false, true)
        };

        var composite = _svc.CalculateCompositeResult(ca, exam, 30, 70, AbsentHandling.ExcludeExceptExamAsZero);
        // Should return exam average only, pro-rated
        Assert.Equal(80m, composite);
    }

    [Fact]
    public void Composite_Absent_For_Examination_Counts_As_Zero()
    {
        var ca = new List<AssessmentScore>
        {
            new AssessmentScore(1, 70, 100, false, false)
        };
        var exam = new List<AssessmentScore>
        {
            new AssessmentScore(2, null, 100, true, true) // absent for examination
        };

        var compositeExclude = _svc.CalculateCompositeResult(ca, exam, 30, 70, AbsentHandling.ExcludeAll);
        // CA avg 70, exam excluded => returns CA only 70
        Assert.Equal(70m, compositeExclude);

        var compositeZero = _svc.CalculateCompositeResult(ca, exam, 30, 70, AbsentHandling.ExcludeExceptExamAsZero);
        // CA 70*0.3=21, exam absent 0*0.7=0 => 21
        Assert.Equal(21m, compositeZero);
    }

    // Term results with subjects a learner does not take
    [Fact]
    public void TermResult_Excludes_Subjects_Not_Taken()
    {
        var subjects = new List<SubjectResult>
        {
            new SubjectResult(1, "Math", 80m, true),
            new SubjectResult(2, "English", 70m, true),
            new SubjectResult(3, "History", null, false), // not taken
            new SubjectResult(4, "Geography", 60m, true)
        };

        var result = _svc.CalculateTermResult(subjects, excludeSubjectsNotTaken: true);
        // Should average only taken: (80+70+60)/3 = 70
        Assert.Equal(210m, result.Aggregate); // 80+70+60
        Assert.Equal(70m, result.Average);
    }

    [Fact]
    public void TermResult_Includes_All_If_Not_Excluded()
    {
        var subjects = new List<SubjectResult>
        {
            new SubjectResult(1, "Math", 80m, true),
            new SubjectResult(2, "English", null, false) // not taken, null score
        };

        var resultInclude = _svc.CalculateTermResult(subjects, excludeSubjectsNotTaken: false);
        // If not excluded, null composite treated as 0? Actually we filter HasValue, so null excluded anyway
        // Our implementation filters HasValue, so even if not excluded, null won't count
        Assert.Equal(80m, resultInclude.Aggregate);
    }

    // Class position with ties, configurable tie rule
    [Fact]
    public void Position_Ties_1224_Standard_Competition()
    {
        var aggregates = new List<StudentTermAggregate>
        {
            new StudentTermAggregate(1, 300, 75),
            new StudentTermAggregate(2, 280, 70),
            new StudentTermAggregate(3, 280, 70), // tie with 2
            new StudentTermAggregate(4, 260, 65)
        };

        var positions = _svc.CalculatePositions(aggregates, positionBy: "average", tieRule: "1224");

        // Average: 75 rank1, 70 rank2 tie, next rank 4 (skips 3)
        Assert.Equal(1, positions.First(p=>p.StudentId==1).Rank);
        Assert.Equal(2, positions.First(p=>p.StudentId==2).Rank);
        Assert.Equal(2, positions.First(p=>p.StudentId==3).Rank);
        Assert.Equal(4, positions.First(p=>p.StudentId==4).Rank);
        Assert.True(positions.First(p=>p.StudentId==2).IsTie);
    }

    [Fact]
    public void Position_Ties_1223_Dense()
    {
        var aggregates = new List<StudentTermAggregate>
        {
            new StudentTermAggregate(1, 300, 75),
            new StudentTermAggregate(2, 280, 70),
            new StudentTermAggregate(3, 280, 70),
            new StudentTermAggregate(4, 260, 65)
        };

        var positions = _svc.CalculatePositions(aggregates, positionBy: "average", tieRule: "1223");

        // Dense: 1,2,2,3
        Assert.Equal(1, positions.First(p=>p.StudentId==1).Rank);
        Assert.Equal(2, positions.First(p=>p.StudentId==2).Rank);
        Assert.Equal(2, positions.First(p=>p.StudentId==3).Rank);
        Assert.Equal(3, positions.First(p=>p.StudentId==4).Rank);
    }

    [Fact]
    public void Position_By_Aggregate_Vs_Average_Different_Subject_Set()
    {
        // Learner A takes 8 subjects aggregate 600 average 75, B takes 6 subjects aggregate 480 average 80
        // By aggregate A wins, by average B wins
        var aggregates = new List<StudentTermAggregate>
        {
            new StudentTermAggregate(1, 600, 75), // 8 subjects
            new StudentTermAggregate(2, 480, 80)  // 6 subjects
        };

        var byAggregate = _svc.CalculatePositions(aggregates, positionBy: "aggregate", tieRule: "1224");
        Assert.Equal(1, byAggregate.First(p=>p.StudentId==1).Rank); // A rank1 by aggregate
        Assert.Equal(2, byAggregate.First(p=>p.StudentId==2).Rank);

        var byAverage = _svc.CalculatePositions(aggregates, positionBy: "average", tieRule: "1224");
        Assert.Equal(1, byAverage.First(p=>p.StudentId==2).Rank); // B rank1 by average
        Assert.Equal(2, byAverage.First(p=>p.StudentId==1).Rank);
    }

    // Promotion rules based on aggregate, subject minimums and attendance
    [Fact]
    public void Promotion_Aggregate_SubjectMinimums_Attendance()
    {
        var rule = new PromotionRule(
            AcademicYearId: 2026,
            FromGradeId: 5,
            ToGradeId: 6,
            MinimumAggregate: 50,
            MinimumAverage: 50,
            MinimumAttendancePercentage: 75,
            MaxFailedSubjects: 2,
            RequiredSubjectsJson: null,
            MinimumSubjectScore: 40
        );

        var subjects = new List<SubjectResult>
        {
            new SubjectResult(1, "Math", 30m, true), // fail <40
            new SubjectResult(2, "English", 60m, true),
            new SubjectResult(3, "Science", 55m, true)
        };

        var decision = _svc.EvaluatePromotion(
            studentId: 1,
            aggregate: 145m, // 30+60+55
            average: 48.33m, // fails average 50
            subjectResults: subjects,
            attendancePercentage: 80m,
            rule: rule
        );

        // Should recommend repeat or conditional because average <50 and failed Math
        Assert.Contains("Average", decision.Reason);
        Assert.Contains("Failed Math", decision.Reason);
        Assert.True(decision.FailedSubjectsCount == 1);
        Assert.Equal("conditional", decision.RecommendedAction); // 1 fail <= max 2 => conditional
    }

    [Fact]
    public void Promotion_Attendance_Fails()
    {
        var rule = new PromotionRule(2026,5,6,50,50,75,2,null,40);
        var subjects = new List<SubjectResult>
        {
            new SubjectResult(1, "Math", 60m, true),
            new SubjectResult(2, "English", 60m, true)
        };

        var decision = _svc.EvaluatePromotion(1, 120, 60, subjects, 70, rule); // attendance 70 <75
        Assert.Contains("Attendance", decision.Reason);
    }

    // Historical analysis: learners whose performance dropped between terms
    [Fact]
    public void Historical_Drop_Detection()
    {
        var histories = new List<StudentTermHistory>
        {
            new StudentTermHistory(1, 2025, 1, 80, 320),
            new StudentTermHistory(1, 2025, 2, 65, 260), // drop 15%
            new StudentTermHistory(2, 2025, 1, 70, 280),
            new StudentTermHistory(2, 2025, 2, 68, 272), // drop 2% not flagged if threshold 10
        };

        var drops = _svc.DetectPerformanceDrops(histories, dropThreshold: 10m);
        Assert.Single(drops);
        Assert.Equal(1, drops[0].StudentId);
        Assert.Equal(-15m, drops[0].Drop);
    }

    // Edge cases: learner who takes different subject set, joined mid-year, absent for examination, transferred with prior results
    [Fact]
    public void Edge_Different_Subject_Set_Excluded_From_Aggregate()
    {
        var subjects = new List<SubjectResult>
        {
            new SubjectResult(1, "Math", 70m, true),
            new SubjectResult(2, "English", 70m, true),
            new SubjectResult(3, "Shona", null, false) // not taken
        };

        var result = _svc.CalculateTermResult(subjects, excludeSubjectsNotTaken: true);
        Assert.Equal(140m, result.Aggregate);
        Assert.Equal(70m, result.Average); // only 2 subjects
    }

    [Fact]
    public void Edge_Joined_MidYear_Average_Of_Attempted_Only()
    {
        var subjects = new List<SubjectResult>
        {
            new SubjectResult(1, "Math", 80m, true, true), // isMidYearJoiner true but still taken
            new SubjectResult(2, "English", null, true, true) // missing because joined late
        };

        var result = _svc.CalculateTermResult(subjects, excludeSubjectsNotTaken: true, isMidYearJoiner: true);
        // Only Math counts => average 80
        Assert.Equal(80m, result.Average);
        Assert.True(result.IsMidYearJoiner);
    }

    [Fact]
    public void Edge_Absent_For_Examination_Treated_As_Zero_When_Policy_ExamZero()
    {
        var ca = new List<AssessmentScore> { new AssessmentScore(1, 70, 100, false, false) };
        var exam = new List<AssessmentScore> { new AssessmentScore(2, null, 100, true, true) }; // absent exam

        var composite = _svc.CalculateCompositeResult(ca, exam, 30, 70, AbsentHandling.ExcludeExceptExamAsZero);
        // CA 70*0.3=21 + exam 0*0.7=0 =>21
        Assert.Equal(21m, composite);
    }

    [Fact]
    public void Edge_Transferred_With_Prior_Results_Transcript()
    {
        var current = new List<SubjectResult>
        {
            new SubjectResult(1, "Math", 70m, true),
            new SubjectResult(2, "English", 60m, true)
        };
        var prior = new List<SubjectResult>
        {
            new SubjectResult(1, "Math", 65m, true),
            new SubjectResult(2, "English", 55m, true)
        };

        // Not include prior in aggregate
        var resultWithoutPrior = _svc.CalculateWithTransferResults(current, prior, includePriorInAggregate: false);
        Assert.Equal(130m, resultWithoutPrior.Aggregate); // 70+60

        // Include prior
        var resultWithPrior = _svc.CalculateWithTransferResults(current, prior, includePriorInAggregate: true);
        Assert.Equal(250m, resultWithPrior.Aggregate); // 70+60+65+55
    }
}
