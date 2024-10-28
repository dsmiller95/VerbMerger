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
            return persistedOutput;
        }
        
        logger.LogInformation("Cache miss for {Input}", input);

        var output = await proompter.Prompt(input);
        await persistence.PersistOutput(input, output);
        return output;
    }
}