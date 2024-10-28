using System.Text.Json.Serialization;
using VerbMerger.Merger.Persistence;

namespace VerbMerger.Merger;

public record MergeInput(string Subject, string Verb, string Object);

public record MergeOutput(string Word, PartOfSpeech PartOfSpeech);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PartOfSpeech
{
    Verb,
    Noun
}

public interface IMergerRepository
{
    public Task<MergeOutput> GetOutput(MergeInput input);
}

public class MergerRepository(
    ILogger<MergerRepository> logger,
    IMergePersistence persistence,
    IMergerProompter proompter
    ) : IMergerRepository
{
    public async Task<MergeOutput> GetOutput(MergeInput input)
    {
        var persistedOutput = await persistence.GetPersistedOutput(input);
        if (persistedOutput != null)
        {
            logger.LogInformation("Cache hit for {Input}", input);
            return persistedOutput;
        }
        
        logger.LogInformation("Cache miss for {Input}", input);

        var promptInput = new[] { input };
        var promptOutput = await proompter.PromptBatch(promptInput);
        var output = promptOutput.Single();
        await persistence.PersistOutput(input, output);
        return output;
    }
}