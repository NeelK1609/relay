namespace Relay.Domain;

/// <summary>What the AI triage agent concluded about a case. Suggestions are kept even when not applied.</summary>
public sealed record CaseInsights(
    string Summary,
    Sentiment Sentiment,
    CaseCategory SuggestedCategory,
    CasePriority SuggestedPriority,
    double Confidence,
    bool SafetyRisk,
    string Reason,
    bool Applied,
    DateTimeOffset AnalyzedAt);
