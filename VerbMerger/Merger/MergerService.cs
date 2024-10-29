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

public interface IMergerService
{
    public Task<MergeOutput> GetOutput(MergeInput input);
}

public class MergerService(
    ILogger<MergerService> logger,
    IMergeRepository repository,
    IMergerProompter proompter
    ) : IMergerService
{
    public async Task<MergeOutput> GetOutput(MergeInput input)
    {
        var persistedOutput = await repository.FindOutput(input);
        if (persistedOutput != null)
        {
            return persistedOutput;
        }
        
        logger.LogInformation("Cache miss for {Input}", input);

        var output = await proompter.Prompt(input);
        await repository.SetOutput(input, output);
        return output;
    }
}