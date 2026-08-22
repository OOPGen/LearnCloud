namespace LearnCloud.Examinations.Services;

/// <summary>
/// Single source of truth for all grading and aggregation logic - ALL calculations in tested services
/// Handles edge cases: different subject set, joined mid-year, absent for examination, transferred with prior results
/// </summary>
public class ExaminationCalculationService
{
    // Grading
    public string? GetGradeFromScale(decimal percentage, List<GradingBand> scale)
    {
        // Scale assumed sorted by lower bound, no overlap, covers 0-100, validated
        // Rounding: use exact decimal 2 places, then lookup
        var rounded = Math.Round(percentage, 2, MidpointRounding.AwayFromZero);
        foreach (var band in scale.OrderBy(b => b.Lower))
        {
            if (rounded >= band.Lower && rounded <= band.Upper)
                return band.Symbol;
        }
        return null; // gap - should not happen if scale validated
    }

    // Weighted composite results: CA vs Exam, weights configurable per subject and per level
    public decimal CalculateCompositeResult(
        List<AssessmentScore> continuousAssessments,
        List<AssessmentScore> examinationAssessments,
        decimal caWeight, // e.g. 30
        decimal examWeight, // e.g. 70
        AbsentHandling absentHandling = AbsentHandling.ExcludeExceptExamAsZero)
    {
        // Normalize each assessment to percentage: score/max *100
        // Absent handling: distinct from zero
        var caPercentages = new List<decimal>();
        foreach (var a in continuousAssessments)
        {
            if (a.IsAbsent)
            {
                if (absentHandling == AbsentHandling.ExcludeAll) continue;
                if (absentHandling == AbsentHandling.CountAsZero) caPercentages.Add(0m);
                else if (absentHandling == AbsentHandling.ExcludeExceptExamAsZero) continue; // CA absent excluded
                else if (absentHandling == AbsentHandling.ExcludeExceptExamAsZero && a.IsExamination) caPercentages.Add(0m);
            }
            else
            {
                if (a.MaxScore <= 0) continue;
                var perc = Math.Round((a.Score ?? 0m) / a.MaxScore * 100m, 2, MidpointRounding.AwayFromZero);
                caPercentages.Add(perc);
            }
        }

        var examPercentages = new List<decimal>();
        foreach (var a in examinationAssessments)
        {
            if (a.IsAbsent)
            {
                if (absentHandling == AbsentHandling.ExcludeAll) continue;
                if (absentHandling == AbsentHandling.CountAsZero) examPercentages.Add(0m);
                else if (absentHandling == AbsentHandling.ExcludeExceptExamAsZero) examPercentages.Add(0m); // exam absent = 0 per policy
            }
            else
            {
                if (a.MaxScore <= 0) continue;
                var perc = Math.Round((a.Score ?? 0m) / a.MaxScore * 100m, 2, MidpointRounding.AwayFromZero);
                examPercentages.Add(perc);
            }
        }

        // Average within each group (CA and Exam)
        decimal caAverage = caPercentages.Any() ? Math.Round(caPercentages.Average(), 2, MidpointRounding.AwayFromZero) : 0m;
        decimal examAverage = examPercentages.Any() ? Math.Round(examPercentages.Average(), 2, MidpointRounding.AwayFromZero) : 0m;

        // If one group empty (e.g. joined mid-year missing CA), handle:
        // If CA empty, return exam average only (pro-rate)
        // If Exam empty, return CA average only
        if (!caPercentages.Any() && examPercentages.Any()) return examAverage;
        if (caPercentages.Any() && !examPercentages.Any()) return caAverage;
        if (!caPercentages.Any() && !examPercentages.Any()) return 0m;

        // Weighted composite: caAverage*caWeight/100 + examAverage*examWeight/100
        var composite = Math.Round(caAverage * caWeight / 100m + examAverage * examWeight / 100m, 2, MidpointRounding.AwayFromZero);
        return composite;
    }

    // Term results per subject, then aggregate
    public TermResult CalculateTermResult(
        List<SubjectResult> subjectResults,
        bool excludeSubjectsNotTaken = true,
        bool isMidYearJoiner = false,
        DateTime? enrolmentDate = null,
        DateTime? termStartDate = null)
    {
        // Filter subjects not taken: if score null and learner does not take subject, exclude
        var takenSubjects = excludeSubjectsNotTaken
            ? subjectResults.Where(s => s.IsTaken && s.CompositeScore.HasValue).ToList()
            : subjectResults.Where(s => s.CompositeScore.HasValue).ToList();

        // Mid-year joiner: average of attempted only already handled by filtering HasValue
        // If isMidYearJoiner and attempted < 50% subjects, flag

        decimal aggregate = takenSubjects.Sum(s => s.CompositeScore ?? 0m);
        decimal average = takenSubjects.Any() ? Math.Round(takenSubjects.Average(s => s.CompositeScore ?? 0m), 2, MidpointRounding.AwayFromZero) : 0m;
        aggregate = Math.Round(aggregate, 2, MidpointRounding.AwayFromZero);

        return new TermResult(takenSubjects, aggregate, average, isMidYearJoiner, enrolmentDate, termStartDate);
    }

    // Class position with ties, configurable tie rule and position by aggregate or average
    public List<PositionResult> CalculatePositions(
        List<StudentTermAggregate> aggregates,
        string positionBy = "average", // aggregate or average
        string tieRule = "1224", // 1224 standard competition, 1223 dense
        bool excludeMidYearJoiners = true,
        Func<StudentTermAggregate, bool>? isMidYearJoiner = null)
    {
        // Filter mid-year joiners if requested
        var filtered = excludeMidYearJoiners && isMidYearJoiner != null
            ? aggregates.Where(a => !isMidYearJoiner(a)).ToList()
            : aggregates.ToList();

        // Sort by chosen metric descending
        var sorted = positionBy == "aggregate"
            ? filtered.OrderByDescending(a => a.Aggregate).ThenBy(a => a.StudentId).ToList()
            : filtered.OrderByDescending(a => a.Average).ThenBy(a => a.StudentId).ToList();

        var results = new List<PositionResult>();
        int rank = 0;
        int position = 0;
        decimal? previousScore = null;
        int sameScoreCount = 0;

        for (int i = 0; i < sorted.Count; i++)
        {
            var current = sorted[i];
            var currentScore = positionBy == "aggregate" ? current.Aggregate : current.Average;

            position++;
            if (previousScore == null || currentScore != previousScore)
            {
                // New score - rank = position (1224) or rank+1 (dense)
                if (tieRule == "1224")
                {
                    rank = position; // standard competition: rank = position, next after tie skips
                }
                else // dense 1223
                {
                    rank = rank + 1;
                }
                sameScoreCount = 1;
            }
            else
            {
                // Tie - same rank as previous
                sameScoreCount++;
            }

            results.Add(new PositionResult(current.StudentId, currentScore, rank, sameScoreCount > 1));

            previousScore = currentScore;
        }

        // Add back excluded mid-year joiners with no position
        if (excludeMidYearJoiners && isMidYearJoiner != null)
        {
            var excluded = aggregates.Where(a => isMidYearJoiner(a)).ToList();
            foreach (var ex in excluded)
            {
                var score = positionBy == "aggregate" ? ex.Aggregate : ex.Average;
                results.Add(new PositionResult(ex.StudentId, score, null, false, true, "Joined mid-year"));
            }
        }

        return results;
    }

    // Promotion decision based on aggregate, subject minimums and attendance
    public PromotionDecisionResult EvaluatePromotion(
        long studentId,
        decimal aggregate,
        decimal average,
        List<SubjectResult> subjectResults,
        decimal attendancePercentage,
        PromotionRule rule)
    {
        var failedSubjects = new List<SubjectResult>();
        var reasons = new List<string>();

        // Aggregate check
        if (aggregate < rule.MinimumAggregate)
        {
            reasons.Add($"Aggregate {aggregate}% < minimum {rule.MinimumAggregate}%");
        }
        if (rule.MinimumAverage.HasValue && average < rule.MinimumAverage.Value)
        {
            reasons.Add($"Average {average}% < minimum {rule.MinimumAverage.Value}%");
        }

        // Attendance check
        if (rule.MinimumAttendancePercentage.HasValue && attendancePercentage < rule.MinimumAttendancePercentage.Value)
        {
            reasons.Add($"Attendance {attendancePercentage}% < minimum {rule.MinimumAttendancePercentage.Value}%");
        }

        // Subject minimums
        foreach (var subj in subjectResults.Where(s => s.IsTaken && s.CompositeScore.HasValue))
        {
            if (rule.MinimumSubjectScore.HasValue && subj.CompositeScore < rule.MinimumSubjectScore.Value)
            {
                failedSubjects.Add(subj);
                reasons.Add($"Failed {subj.SubjectName} {subj.CompositeScore}% < minimum {rule.MinimumSubjectScore.Value}%");
            }

            // Required subjects
            if (!string.IsNullOrEmpty(rule.RequiredSubjectsJson))
            {
                var required = System.Text.Json.JsonSerializer.Deserialize<List<long>>(rule.RequiredSubjectsJson);
                if (required != null && required.Contains(subj.SubjectId) && subj.CompositeScore < (rule.MinimumSubjectScore ?? 40m))
                {
                    if (!failedSubjects.Contains(subj))
                        failedSubjects.Add(subj);
                    reasons.Add($"Required subject {subj.SubjectName} failed {subj.CompositeScore}%");
                }
            }
        }

        // Max failed subjects
        if (rule.MaxFailedSubjects.HasValue && failedSubjects.Count > rule.MaxFailedSubjects.Value)
        {
            reasons.Add($"Failed {failedSubjects.Count} subjects > max allowed {rule.MaxFailedSubjects.Value}");
        }

        var recommended = reasons.Any() ? (failedSubjects.Count <= (rule.MaxFailedSubjects ?? 2) ? "conditional" : "repeat") : "promoted";
        if (reasons.Count == 0) recommended = "promoted";

        return new PromotionDecisionResult(studentId, aggregate, average, failedSubjects.Count, attendancePercentage, recommended, string.Join("; ", reasons), failedSubjects);
    }

    // Historical analysis: learners whose performance dropped between terms
    public List<PerformanceDrop> DetectPerformanceDrops(
        List<StudentTermHistory> histories,
        decimal dropThreshold = 10m) // drop >10% flagged
    {
        var drops = new List<PerformanceDrop>();

        var groupedByStudent = histories.GroupBy(h => h.StudentId);
        foreach (var group in groupedByStudent)
        {
            var ordered = group.OrderBy(h => h.AcademicYearId).ThenBy(h => h.TermId).ToList();
            for (int i = 1; i < ordered.Count; i++)
            {
                var prev = ordered[i - 1];
                var curr = ordered[i];
                var diff = curr.Average - prev.Average;
                if (diff <= -dropThreshold)
                {
                    drops.Add(new PerformanceDrop(
                        curr.StudentId,
                        prev.AcademicYearId,
                        prev.TermId,
                        prev.Average,
                        curr.AcademicYearId,
                        curr.TermId,
                        curr.Average,
                        diff,
                        $"Dropped {Math.Abs(diff)}% from {prev.Average}% to {curr.Average}%"
                    ));
                }
            }
        }

        return drops;
    }

    // Transferred from another school with prior results - incorporate prior results as separate history
    public TermResult CalculateWithTransferResults(
        List<SubjectResult> currentResults,
        List<SubjectResult> priorResults, // from previous school
        bool includePriorInAggregate = false)
    {
        // For V1, prior results not included in current term aggregate, but shown in transcript
        // Transcript will show both current and prior
        if (includePriorInAggregate)
        {
            var combined = currentResults.Concat(priorResults).ToList();
            return CalculateTermResult(combined);
        }
        else
        {
            return CalculateTermResult(currentResults);
        }
    }
}

// Supporting DTOs for calculation service - pure, no EF

public enum AbsentHandling
{
    ExcludeAll, // absent excluded from average
    CountAsZero, // absent = 0
    ExcludeExceptExamAsZero // CA absent excluded, exam absent =0 (recommended)
}

public record AssessmentScore(long AssessmentId, decimal? Score, decimal MaxScore, bool IsAbsent, bool IsExamination);

public record GradingBand(string Symbol, string Description, decimal Lower, decimal Upper);

public record SubjectResult(long SubjectId, string SubjectName, decimal? CompositeScore, bool IsTaken, bool IsMidYearJoiner = false);

public record TermResult(List<SubjectResult> SubjectResults, decimal Aggregate, decimal Average, bool IsMidYearJoiner, DateTime? EnrolmentDate, DateTime? TermStartDate);

public record StudentTermAggregate(long StudentId, decimal Aggregate, decimal Average);

public record PositionResult(long StudentId, decimal Score, int? Rank, bool IsTie, bool IsExcludedFromPosition = false, string? Reason = null);

public record PromotionRule(
    long AcademicYearId,
    long FromGradeId,
    long ToGradeId,
    decimal MinimumAggregate,
    decimal? MinimumAverage,
    decimal? MinimumAttendancePercentage,
    int? MaxFailedSubjects,
    string? RequiredSubjectsJson,
    decimal? MinimumSubjectScore
);

public record PromotionDecisionResult(
    long StudentId,
    decimal Aggregate,
    decimal Average,
    int FailedSubjectsCount,
    decimal AttendancePercentage,
    string RecommendedAction,
    string Reason,
    List<SubjectResult> FailedSubjects
);

public record StudentTermHistory(long StudentId, long AcademicYearId, long TermId, decimal Average, decimal Aggregate);

public record PerformanceDrop(
    long StudentId,
    long PreviousAcademicYearId,
    long PreviousTermId,
    decimal PreviousAverage,
    long CurrentAcademicYearId,
    long CurrentTermId,
    decimal CurrentAverage,
    decimal Drop,
    string Reason
);
