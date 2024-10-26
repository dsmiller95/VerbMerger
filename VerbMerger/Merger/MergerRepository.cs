using System.Text.Json.Serialization;

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

public class MergerRepository : IMergerRepository
{
    public async Task<MergeOutput> GetOutput(MergeInput input)
    {
        var rng = new Random();
        return rng.Next(0, 3) switch
        {
            0 => new MergeOutput(input.Subject, PartOfSpeech.Noun),
            1 => new MergeOutput(input.Verb, PartOfSpeech.Verb),
            2 => new MergeOutput(input.Object, PartOfSpeech.Noun),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}