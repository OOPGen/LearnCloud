using System.Text;
using System.Text.Json;
using LearnCloud.AI.Services.Providers;

namespace LearnCloud.AI.Services.Providers;

// Optional LLM integration - swappable via AIProviderSettings, fallback to RuleBased if no API key
public class OpenAIProvider : IAIProvider
{
    public string ProviderName => "OpenAI";
    private readonly string _apiKey;
    private readonly string _model;
    private readonly HttpClient _http;
    private readonly ILogger<OpenAIProvider> _logger;

    public OpenAIProvider(string apiKey, string model, HttpClient http, ILogger<OpenAIProvider> logger)
    {
        _apiKey = apiKey;
        _model = model;
        _http = http;
        _logger = logger;
    }

    public async Task<CommentGenerationResult> GenerateReportCommentAsync(CommentGenerationInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            // Fallback to rule-based if no key
            var fallback = new RuleBasedAIProvider();
            return await fallback.GenerateReportCommentAsync(input, ct);
        }

        var systemPrompt = $@"You are an experienced Zimbabwean school teacher writing report card comments. 
Tone: {input.Tone}. Length: {input.Length}. 
Rules: Be specific, mention subjects by name, mention attendance, be encouraging but honest, avoid banned words revolutionary/cutting-edge/seamless/empower.
Never write automatically - this is draft for teacher review.
Student: {input.StudentName}, Grade {input.GradeName} {input.StreamName}, Average {input.Average:F1}%, Aggregate, Position {input.Position}, Attendance {input.Attendance.Percentage:F1}% ({input.Attendance.Present} present, {input.Attendance.Absent} absent).
Subjects: {string.Join(", ", input.SubjectPerformances.Select(s => $"{s.SubjectName}: {s.Score?.ToString("F0") ?? "N/A"}% Grade {s.Grade} Trend {s.Trend}"))}
Custom instructions: {input.CustomInstructions}
Write one paragraph {input.Length} length comment in {input.Tone} tone.";

        var userPrompt = $"Draft report comment for {input.StudentName}";

        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.7,
            max_tokens = input.Length == "short" ? 100 : input.Length == "long" ? 300 : 200
        };

        var json = JsonSerializer.Serialize(requestBody);
        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("Authorization", $"Bearer {_apiKey}");

        var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI failed {Status}, falling back to rule-based", resp.StatusCode);
            var fallback = new RuleBasedAIProvider();
            return await fallback.GenerateReportCommentAsync(input, ct);
        }

        var respJson = await resp.Content.ReadAsStringAsync(ct);
        try
        {
            using var doc = JsonDocument.Parse(respJson);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
            var usage = doc.RootElement.TryGetProperty("usage", out var u) ? u : default;
            int promptTokens = 0, completionTokens = 0;
            if (usage.ValueKind != JsonValueKind.Undefined)
            {
                promptTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
                completionTokens = usage.TryGetProperty("completion_tokens", out var ct2) ? ct2.GetInt32() : 0;
            }

            return new CommentGenerationResult
            {
                DraftComment = content.Trim(),
                ProviderName = ProviderName,
                Model = _model,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                Cost = 0.01m // simplified cost calc
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse OpenAI response, fallback to rule-based");
            var fallback = new RuleBasedAIProvider();
            return await fallback.GenerateReportCommentAsync(input, ct);
        }
    }
}

// Factory swappable without touching calling code
public interface IAIProviderFactory
{
    Task<IAIProvider> GetProviderAsync(long tenantId, CancellationToken ct = default);
}

public class AIProviderFactory : IAIProviderFactory
{
    private readonly LearnCloud.MultiTenancy.Context.LearnCloudDbContext _db;
    private readonly IServiceProvider _sp;

    public AIProviderFactory(LearnCloud.MultiTenancy.Context.LearnCloudDbContext db, IServiceProvider sp)
    {
        _db = db;
        _sp = sp;
    }

    public async Task<IAIProvider> GetProviderAsync(long tenantId, CancellationToken ct = default)
    {
        var settings = await _db.Set<Entities.AIProviderSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.IsActive && s.IsDefault && !s.IsDeleted, ct);
        var providerName = settings?.ProviderName ?? "RuleBased";

        return providerName switch
        {
            "OpenAI" => (IAIProvider)_sp.GetService(typeof(OpenAIProvider))!,
            _ => (IAIProvider)_sp.GetService(typeof(RuleBasedAIProvider))!
        };
    }
}
