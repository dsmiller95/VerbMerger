namespace VerbMerger.Merger;

public class VerbMergerConfig
{
    public float ArtificialPromptDelaySeconds { get; set; } = 0f;
    public float SystemPromptCacheTimeSeconds { get; set; } = 60f;
    public int SystemPromptExampleSampleCount { get; set; } = 50;
    
    public int PromptMaxBatchSize { get; set; } = 30;
    public int PromptBatchIntervalMs { get; set; } = 5000;
}