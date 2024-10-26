using OpenAI.Interfaces;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace VerbMerger.Merger;

public interface IMergerProompter
{
    public Task<IEnumerable<MergeOutput>> PromptBatch(IEnumerable<MergeInput> input);
}

public class MergerProompter(IOpenAIService aiService, ILogger<MergerProompter> logger) : IMergerProompter
{
    private const string SystemPrompt = @"
You are an alchemical wizard, and also a fluent storyteller. You want to tell the story of the world you have grown up in. You can only respond in very specific ways because of a potion which went wrong after you formulated it. You want to communicate how the world works, as well as tell a story about how you got to where you are. You can only respond in very specific ways.

Your students will give you a 3-word sentence, and you will tell them the result of it. For example, your student might say ""Fire Mix Water"", and you would respond with ""Steam"".
Or your student will say ""Mud Mix Fire"", and you would respond with ""Harden"". you can only respond with a noun or a verb, picking whichever one is most interesting and communicates the most about the world and your story.

the requests will arrive in a batch. You will respond with a delimited list of responses to each request in the batch. do not respond with anything other than what matches this format.
Here are examples of the format of the requests and responses. 


Request:

Cloud | Condense | Rain
Stone | Crumble | Earth
Mud | Shape | Brick
Plant | Grow | Air
Fire | Ignite | Wood
Glass | Fuse | Sand
Lava | Solidify | Water
River | Flow | Earth
Ash | Scatter | Air
Lightning | Electrify | Cloud

Your Response:

River | Noun
Gravel | Noun
Sculpture | Noun
Forest | Noun
Ember | Noun
Mirror | Noun
Obsidian | Noun
Erode | Verb
Soot | Noun
Charge | Verb

Request:

Obsidian | Harden | Stone
Forest | Breathe | Mist
Ember | Form | Lava
Dust | Scatter | Fire
Dew | Condense | Leaf

Your Response:

Blade | Noun
Dew | Noun
Glow | Verb
Flicker | Verb
Drip | Verb

Request:
";

    public async Task<IEnumerable<MergeOutput>> PromptBatch(IEnumerable<MergeInput> input)
    {
        var userPrompt = GetPrompt(input);
        logger.LogInformation("Prompting with {Prompt}", userPrompt);
        var completionResult = await aiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem(SystemPrompt),
                ChatMessage.FromUser(userPrompt)
            },
            Model = Models.Gpt_4o_mini,
            Temperature = 1,
            MaxTokens = 2048,
            N = 1,
        });

        if (!completionResult.Successful)
        {
            throw new Exception($"Failed to get completion: {completionResult.Error?.Message ?? "null message"}");
        }

        var completionText = completionResult.Choices.Single().Message.Content;
        logger.LogInformation("Completion: {Completion}", completionText);
        if (completionText == null) throw new Exception("completion result string is null");
        
        return GetParsedResponse(completionText);
    }
    
    private string GetPrompt(IEnumerable<MergeInput> inputBatch)
    {
        return string.Join("\n", inputBatch.Select(x => $"{x.Subject} | {x.Verb} | {x.Object}"));
    }
    
    private IEnumerable<MergeOutput> GetParsedResponse(string completionResponse)
    {
        return completionResponse.Split("\n").Select(x =>
        {
            var split = x.Split(" | ");
            var partOfSpeech = split[1] switch
            {
                "Noun" => PartOfSpeech.Noun,
                "Verb" => PartOfSpeech.Verb,
                _ => throw new ArgumentOutOfRangeException(split[1])
            };
            return new MergeOutput(split[0], partOfSpeech);
        });
    }
    
    
}