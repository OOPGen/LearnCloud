namespace LearnCloud.AI.QueryAssistant;

public record NaturalLanguageQueryRequest(
    string Query, // e.g. "How many students have arrears over $100 in Grade 5?"
    bool IncludePersonalData, // must match opt-in
    string? Context // optional additional context
);

public record NaturalLanguageQueryResponse(
    long LogId,
    string NaturalLanguageQuery,
    string InterpretedQuery, // human readable structured interpretation
    string SqlQuery, // read-only SELECT with tenant_id enforced
    List<string> Inputs, // what inputs were used
    string Reasoning, // explanation of how query was interpreted, tentative language
    List<Dictionary<string, object>> Results, // aggregated results, no personal data unless opt-in
    int ResultCount,
    string ProviderName,
    string? Model,
    decimal Cost,
    bool RequiresApproval, // always true - human approves before anything saved or sent
    bool IsDismissible, // every output can be dismissed
    string TentativeNotice, // never present prediction about child as fact, tentative supportive
    bool PersonalDataLeavesSystem, // does personal learner data leave?
    string WhatIsSentPlainly, // plainly what is sent and to which provider
    bool IsOptedIn, // is tenant opted in?
    bool CanSave // can save/send only if human approves
);

public record ApproveQueryRequest(long LogId, bool Approved, string? Feedback);

public record DismissQueryRequest(long LogId, string? Reason);

public record OptInSettingsDto(
    long Id,
    bool IsOptedIn,
    DateTime? OptedInAt,
    bool AllowPersonalLearnerDataToLeave,
    bool HasAcknowledgedWhatIsSent,
    string AcknowledgedNoticeText,
    string? ProviderName,
    string? ProviderDescription,
    bool EnableQueryAssistant,
    DateTime? OptedOutAt,
    string WhatIsSentPlainly // plain language notice
);

public record UpdateOptInRequest(
    bool IsOptedIn,
    bool AllowPersonalLearnerDataToLeave,
    bool HasAcknowledgedWhatIsSent,
    string? ProviderName,
    bool EnableQueryAssistant
);

public record ProviderSettingsDto(long Id, string ProviderName, bool IsActive, bool IsDefault, decimal MonthlyCapUsd, decimal CurrentMonthCost, int CurrentMonthYear, int CurrentMonthMonth, decimal CostPer1000TokensInput, decimal CostPer1000TokensOutput);
public record CostTrackingDto(int Year, int Month, int QueryCount, int PromptTokens, int CompletionTokens, decimal TotalCost, string Currency, decimal Cap, decimal Remaining, bool CapExceeded);
