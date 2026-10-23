namespace Relay.Agents;

public sealed class FoundryOptions
{
    public const string Section = "Foundry";

    /// <summary>Foundry resource endpoint, e.g. https://name.openai.azure.com/. AI features are off when empty.</summary>
    public string? Endpoint { get; set; }
    public string ChatDeployment { get; set; } = "gpt-5-mini";
    public string EmbeddingDeployment { get; set; } = "text-embedding-3-small";

    /// <summary>Triage suggestions below this confidence are recorded but not applied.</summary>
    public double AutoApplyConfidence { get; set; } = 0.75;

    /// <summary>Lets the answer agent reply to customers directly when the help centre fully covers the question.</summary>
    public bool AutoAnswer { get; set; } = true;

    /// <summary>Include prompts and completions in traces. Keep off outside development.</summary>
    public bool TraceSensitiveData { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint);
}
