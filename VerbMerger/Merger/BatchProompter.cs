using System.Text;
using Microsoft.Extensions.Options;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using VerbMerger.Merger.Persistence;

namespace VerbMerger.Merger;

public interface IMergerBatchProompter
{
    public Task<IEnumerable<MergeOutputResult>> PromptBatch(IEnumerable<MergeInput> input, CancellationToken cancellationToken);
}



public class BatchProompter(
    IOpenAIService aiService,
    IMergeSampler sampler,
    IOptions<VerbMergerConfig> options,
    ILogger<BatchProompter> logger
    ) : IMergerBatchProompter
{
    private const string SystemPromptBase = """
You are an alchemical wizard, and also a fluent storyteller. You want to tell the story of the world you have grown up in.
You can only respond in very specific ways because of a potion which went wrong after you formulated it.
Your goal is to communicate how the world works and pass on knowledge of popular culture (pirates of the carribiean, war and peace,
marvel comics, cars, corn, trains, earth, mars, Democracy, Plato, Rome, etc) to your students. When reasonable, be specific and use proper nouns.

Your students will give you a 3-word sentence, and you will tell them the result of it.
For example, your student might say "Water Add Fire", and you would respond with "Steam".
Or your student will say "Mud Add Fire", and you would respond with "Harden".
The Order of the words matters. the first word is what is performing the action, the last word is what the action is being performed on.
"Fire Add Mud" would result in "Extinguish" for example.

You can only respond with a phrase which is either an object ("San fransico", "Fire", "My Thumb") or action ("Add", "Breathe", "Harden"),
 picking whichever one is most interesting and communicates the most about the world.
It may not be a single word but it should never be more than 3. Most of the time you should respond with exactly one word.
If a combination does not make sense, or does not work, then the combination results in a regularly formatted [Subject] | [Verb] | [Object] | Nonsense | Noun

the requests will arrive in a batch. You will respond with a delimited list of responses to each request in the batch. do not respond with anything other than what matches this format.
For every row in the request produce -exactly- one row in the response. do not produce any more or any less. each row should match up exactly.
Here are examples of the format of the requests and responses. DO NOT DEVIATE FROM THIS FORMAT. Always output a table with 5 columns and 4 delimiters. no exceptions.

Request:

Water | Add | Fire
Fire | Add | Water
Earth | Add | Water
Water | Add | Earth
Air | Add | Mud
Fire | Add | Air
Stone | Harden | Obsidian
Stone | Chip | Obsidian
Forest | Breathe | Mist
Ember | Form | Lava

Your Response:

Water | Add | Fire | Steam | Noun
Fire | Add | Water | Extinguish | Verb
Earth | Add | Water | Mud | Noun
Water | Add | Earth | Silt | Noun
Air | Add | Mud | Splatter | Verb
Fire | Add | Air | Bellows | Noun
Stone | Harden | Obsidian | Nonsense | Noun
Stone | Chip | Obsidian | Blade | Noun
Forest | Breathe | Mist | Drink | Verb
Ember | Form | Lava | Melt | Verb

Request:

Spark | Awaken | Earth
Life | Add | Knowledge

Your Response:

Spark | Awaken | Earth | Life | Noun
Life | Add | Knowledge | Human | Noun

Request:

Alchemist's Quest | Seek | Truth Beyond
Stars | Whisper to | Sleeping Gods
Thunderstorm Clouds | Gather Above | Distant Horizon
Ocean Waves | Crash Against | Rocky Shore
Winter Chill | Settles Into | Quiet Lake

Your Response:

Alchemist's Quest | Seek | Truth Beyond | Reality | Noun
Stars | Whisper to | Sleeping Gods | Echoes | Noun
Thunderstorm Clouds | Gather Above | Distant Horizon | Darken | Verb
Ocean Waves | Crash Against | Rocky Shore | Foam | Noun
Winter Chill | Settles Into | Quiet Lake | Freeze | Verb

Following are further examples of what your responses could be:
""";

    private const string UserPromptPostFix = @"

Your Response:
";

    private string? _cachedSystemPrompt = null;
    private DateTime _lastSystemPromptTime = DateTime.MinValue;
    
    
    public async Task<IEnumerable<MergeOutputResult>> PromptBatch(IEnumerable<MergeInput> inputEnum, CancellationToken cancellationToken)
    {
        var filterInput = (await sampler.Filter(inputEnum)).ToList();

        var allowedPromptIndexesInInput = filterInput
            .Select((value, index) => (value, index))
            .Where(x => x.value.Status == FilterStatus.Valid)
            .Select(x => x.index).ToList();
        var allowedPrompts = allowedPromptIndexesInInput.Select(x => filterInput[x].Input).ToList();
        
        var promptResults = 
            (await PromptBatchUnfiltered(allowedPrompts, cancellationToken)).ToList();
        
        var result = filterInput.Select((x, i) =>
        {
            var status = x.Status switch
            {
                FilterStatus.Valid => MergeOutputStatus.Valid,
                FilterStatus.TermMissing => MergeOutputStatus.InputTermNotPreviouslyGenerated,
                _ => throw new ArgumentOutOfRangeException()
            }; 
            return new MergeOutputResult
            {
                Output = null,
                Status = status
            };
        }).ToList();

        for (int i = 0; i < allowedPromptIndexesInInput.Count(); i++)
        {
            var inputIndex = allowedPromptIndexesInInput[i];
            var promptOutput = promptResults[i];

            var res = result[inputIndex];
            if (res.Status != MergeOutputStatus.Valid)
            {
                logger.LogCritical("Invalid status for valid prompt. logic error.");
            }
            res.Output = promptOutput;
            result[inputIndex] = res;
        }
        
        return result;
    }
    
    private async Task<IEnumerable<MergeOutput>> PromptBatchUnfiltered(IEnumerable<MergeInput> input, CancellationToken cancellationToken)
    {
        input = input.ToList();
        
        var userPrompt = GetPrompt(input);
        logger.LogInformation("Prompting with {Prompt}", userPrompt);
        
        var delay = options.Value.ArtificialPromptDelaySeconds;
        var artificialDelay = delay <= 0 ? null : Task.Delay(TimeSpan.FromSeconds(options.Value.ArtificialPromptDelaySeconds), cancellationToken);
        
        var completionResult = await aiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem(await GetSystemPrompt()),
                ChatMessage.FromUser(userPrompt + UserPromptPostFix)
            },
            Model = Models.Gpt_4o_mini,
            Temperature = 1,
            MaxTokens = 2048,
            N = 1,
        }, cancellationToken: cancellationToken);

        if (artificialDelay != null) await artificialDelay;

        if (!completionResult.Successful)
        {
            throw new Exception($"Failed to get completion: {completionResult.Error?.Message ?? "null message"}");
        }

        var completionText = completionResult.Choices.Single().Message.Content;
        logger.LogInformation("Completion: {Completion}", completionText);
        if (completionText == null) throw new Exception("completion result string is null");

        var response = GetParsedResponse(completionText).ToList();
        if (response.Count != input.Count())
        {
            logger.LogError("Got {ResponseCount} responses for {InputCount} inputs", response.Count, input.Count());
        }
        
        return response.Take(input.Count());
    }
    
    private string GetPrompt(IEnumerable<MergeInput> inputBatch)
    {
        return string.Join("\n", inputBatch.Select(x => $"{x.Subject} | {x.Verb} | {x.Object}"));
    }

    private async Task<string> GetSystemPrompt()
    {
        if(this._cachedSystemPrompt != null && 
           DateTime.UtcNow - _lastSystemPromptTime < TimeSpan.FromSeconds(options.Value.SystemPromptCacheTimeSeconds))
        {
            return _cachedSystemPrompt;
        }
        
        var textBuilder = new StringBuilder();
        textBuilder.Append(SystemPromptBase);
        textBuilder.Append("\n");
        var examples = await sampler.SampleExamples(options.Value.SystemPromptExampleSampleCount);
        textBuilder.Append(GetExampleCases(examples));
        textBuilder.Append("\nRequest:\n");
        
        _cachedSystemPrompt = textBuilder.ToString();
        logger.LogInformation("Rebuilt system prompt to cache: {Prompt}", _cachedSystemPrompt);
        _lastSystemPromptTime = DateTime.UtcNow;
        return _cachedSystemPrompt;
    }

    private string GetExampleCases(IEnumerable<MergeResult> exampleBatch)
    {
        return string.Join("\n", exampleBatch.Select(x => $"{x.Input.Subject} | {x.Input.Verb} | {x.Input.Object} | {x.Output.Word} | {x.Output.PartOfSpeech}"));
    }
    
    private IEnumerable<MergeOutput> GetParsedResponse(string completionResponse)
    {
        return completionResponse.Split("\n").Select(x =>
        {
            var split = x.Split(" | ");
            var partOfSpeech = split[4].TrimEnd() switch
            {
                "Noun" => PartOfSpeech.Noun,
                "Verb" => PartOfSpeech.Verb,
                _ => throw new ArgumentOutOfRangeException(split[4])
            };
            return new MergeOutput(split[3], partOfSpeech);
        });
    }
    
    
}