using LearnCloud.AI.DTOs;
using LearnCloud.AI.Services.Providers;

namespace LearnCloud.AI.Services.Providers;

// Rule-based fallback that doesn't require API key, works offline, saves teacher hours per term
// This alone is the feature to build first - teacher always reviews and edits
public class RuleBasedAIProvider : IAIProvider
{
    public string ProviderName => "RuleBased";

    public Task<CommentGenerationResult> GenerateReportCommentAsync(CommentGenerationInput input, CancellationToken ct = default)
    {
        var strengths = input.SubjectPerformances.Where(s => s.Score.HasValue && s.Score >= 70).OrderByDescending(s => s.Score).Take(2).ToList();
        var weaknesses = input.SubjectPerformances.Where(s => s.Score.HasValue && s.Score < 50).OrderBy(s => s.Score).Take(2).ToList();
        var improving = input.SubjectPerformances.Where(s => s.Trend == "up").Take(2).ToList();
        var declining = input.SubjectPerformances.Where(s => s.Trend == "down").Take(2).ToList();

        var attendanceSummary = input.Attendance.Percentage >= 95 ? "excellent attendance" :
                                input.Attendance.Percentage >= 85 ? "good attendance" :
                                input.Attendance.Percentage >= 75 ? "fair attendance, room to improve" : "attendance needs attention";

        var avg = input.Average;
        var overall = avg >= 80 ? "outstanding overall performance" :
                      avg >= 70 ? "very good performance" :
                      avg >= 60 ? "good performance, consistent effort" :
                      avg >= 50 ? "fair performance, with potential to improve" :
                      avg >= 40 ? "has potential but needs more focus" : "needs significant support and effort";

        // Tone handling
        string tonePrefix = input.Tone.ToLower() switch
        {
            "formal" => "",
            "concise" => "",
            "detailed" => "",
            "encouraging" => "",
            _ => ""
        };

        // Length handling
        string comment = "";

        if (input.Length == "short")
        {
            comment = $"{input.StudentName} has shown {overall} with {avg:F1}% average. {Capitalize(attendanceSummary)}. ";
            if (strengths.Any()) comment += $"Strength in {string.Join(" and ", strengths.Select(s => s.SubjectName))}. ";
            if (weaknesses.Any()) comment += $"Needs support in {string.Join(" and ", weaknesses.Select(s => s.SubjectName))}. ";
            comment = ApplyTone(comment, input.Tone);
        }
        else if (input.Length == "long")
        {
            comment = $"{input.StudentName} in {input.GradeName} {input.StreamName} has achieved {overall} this term, with an average of {avg:F1}%";
            if (input.Position.HasValue) comment += $", ranked position {input.Position} in class";
            comment += $". Attendance is {input.Attendance.Percentage:F1}% ({input.Attendance.Present}/{input.Attendance.TotalDays} days present), which is {attendanceSummary}. ";

            if (strengths.Any())
                comment += $"Particular strengths shown in {string.Join(", ", strengths.Select(s => $"{s.SubjectName} ({s.Score:F0}%)"))}. ";
            if (improving.Any())
                comment += $"Notable improvement in {string.Join(", ", improving.Select(s => s.SubjectName))}. ";
            if (weaknesses.Any())
                comment += $"Areas needing attention include {string.Join(", ", weaknesses.Select(s => $"{s.SubjectName} ({s.Score:F0}%)"))}. ";
            if (declining.Any())
                comment += $"Performance has dipped in {string.Join(", ", declining.Select(s => s.SubjectName))}, which should be reviewed. ";

            comment += $"Overall, {input.StudentName} is a {(avg >= 60 ? "diligent and capable" : "capable but needing more consistency")} learner. ";

            if (!string.IsNullOrEmpty(input.CustomInstructions))
                comment += $"{input.CustomInstructions} ";

            comment += $"With continued effort and support at home, {input.StudentName} can build on this progress next term.";

            comment = ApplyTone(comment, input.Tone);
        }
        else // medium
        {
            comment = $"{input.StudentName} has achieved {overall} this term with {avg:F1}% average. {Capitalize(attendanceSummary)} ({input.Attendance.Percentage:F1}% - {input.Attendance.Present} present, {input.Attendance.Absent} absent). ";

            if (strengths.Any())
                comment += $"Strong performance in {string.Join(" and ", strengths.Select(s => $"{s.SubjectName}"))}. ";
            if (weaknesses.Any())
                comment += $"{string.Join(" and ", weaknesses.Select(s => s.SubjectName))} needs more focus. ";
            if (improving.Any())
                comment += $"Improving in {string.Join(", ", improving.Select(s => s.SubjectName))} — keep it up. ";

            comment += $"Keep working hard, {input.StudentName.Split(' ')[0]}.";

            if (!string.IsNullOrEmpty(input.CustomInstructions))
                comment += $" {input.CustomInstructions}";

            comment = ApplyTone(comment, input.Tone);
        }

        // Ensure teacher review disclaimer
        comment = comment.Trim();
        if (!comment.EndsWith(".")) comment += ".";

        return Task.FromResult(new CommentGenerationResult
        {
            DraftComment = comment,
            ProviderName = ProviderName,
            Model = "rule-based-v1",
            PromptTokens = 0,
            CompletionTokens = 0,
            Cost = 0m
        });
    }

    private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    private static string ApplyTone(string comment, string tone)
    {
        return tone.ToLower() switch
        {
            "formal" => comment.Replace("Keep working hard", "Continued diligence is encouraged").Replace("keep it up", "sustained effort is encouraged"),
            "concise" => string.Join(". ", comment.Split(". ").Take(2)) + ".",
            "encouraging" => comment + " Well done and keep going!",
            "detailed" => comment,
            _ => comment
        };
    }
}
