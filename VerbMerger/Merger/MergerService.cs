using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using VerbMerger.Merger.Persistence;

namespace VerbMerger.Merger;

public record MergeInput(string Subject, string Verb, string Object);

/// <summary>
/// Represents the successful result of merging words
/// </summary>
/// <param name="Word"></param>
/// <param name="PartOfSpeech"></param>
public record MergeOutput(string Word, PartOfSpeech PartOfSpeech);

/// <summary>
/// Represents the output of an attempt to merge words. May fail.
/// </summary>
public record struct MergeOutputResult
{
    public static MergeOutputResult Fail(MergeOutputStatus status) => new()
    {
        Status = status
    };
    public static MergeOutputResult Success(MergeOutput output) => new()
    {
        Status = MergeOutputStatus.Valid,
        Output = output
    };
    
    public MergeOutput? Output;
    public MergeOutputStatus Status;
    
    public bool IsSuccess => Status == MergeOutputStatus.Valid;
    public bool TryGetSuccess([NotNullWhen(true)] out MergeOutput? output)
    {
        if(!IsSuccess)
        {
            output = null;
            return false;
        }
        
        ArgumentNullException.ThrowIfNull(Output);
        output = Output;
        return IsSuccess;
    }
}
public enum MergeOutputStatus
{
    Valid,
    InputTermNotPreviouslyGenerated
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PartOfSpeech
{
    Verb,
    Noun
}

public interface IMergerService
{
    public Task<MergeOutputResult> GetOutput(MergeInput input);
}

public class MergerService(
    ILogger<MergerService> logger,
    IMergeRepository repository,
    IMergerProompter proompter
    ) : IMergerService
{
    public async Task<MergeOutputResult> GetOutput(MergeInput input)
    {
        var persistedOutput = await repository.FindOutput(input);
        if (persistedOutput != null)
        {
            return MergeOutputResult.Success(persistedOutput);
        }
        
        logger.LogInformation("Cache miss for {Input}", input);

        var output = await proompter.Prompt(input);

        if (output.TryGetSuccess(out var success))
        {
            await repository.SetOutput(input, success);
        }
        
        return output;
    }
}