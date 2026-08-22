using LearnCloud.AI.DTOs;

namespace LearnCloud.AI.Services.Providers;

public class CommentGenerationInput
{
    public long StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string GradeName { get; set; } = "";
    public string StreamName { get; set; } = "";
    public List<SubjectPerformanceDto> SubjectPerformances { get; set; } = new();
    public AttendanceForCommentDto Attendance { get; set; } = null!;
    public decimal Aggregate { get; set; }
    public decimal Average { get; set; }
    public int? Position { get; set; }
    public string Tone { get; set; } = "encouraging";
    public string Length { get; set; } = "medium";
    public string? CustomInstructions { get; set; }
}

public class CommentGenerationResult
{
    public string DraftComment { get; set; } = null!;
    public string ProviderName { get; set; } = "RuleBased";
    public string? Model { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public decimal Cost { get; set; }
}

public interface IAIProvider
{
    string ProviderName { get; }
    Task<CommentGenerationResult> GenerateReportCommentAsync(CommentGenerationInput input, CancellationToken ct = default);
}
