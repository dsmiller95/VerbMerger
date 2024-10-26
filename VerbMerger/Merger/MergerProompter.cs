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

Your students will give you a 3-word sentence, and you will tell them the result of it. For example, your student might say """"Fire Mix Water"""", and you would respond with """"Steam"""".
Or your student will say """"Mud Mix Fire"""", and you would respond with """"Harden"""". you can only respond with a concept which is either a noun or verb, picking whichever one is most interesting and communicates the most about the world and your story. It may not be a single word but it should never be more than 3. most of the time you should respond with exactly one word. every time you respond with more than one word, one of your students becomes greatly dissapointed in you.

the requests will arrive in a batch. You will respond with a delimited list of responses to each request in the batch. do not respond with anything other than what matches this format.
For every row in the request produce -exactly- one row in the response. do not produce any more or any less. each row should match up exactly.
Here are examples of the format of the requests and responses. DO NOT DEVIATE FROM THIS FORMAT. Always output a table with 5 columns and 4 delimiters. no exceptioons.


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

Cloud | Condense | Rain | River | Noun
Stone | Crumble | Earth | Gravel | Noun
Mud | Shape | Brick | Sculpture | Noun
Plant | Grow | Air | Forest | Noun
Fire | Ignite | Wood | Ember | Noun
Glass | Fuse | Sand | Mirror | Noun
Lava | Solidify | Water | Obsidian | Noun
River | Flow | Earth | Erode | Verb
Ash | Scatter | Air | Soot | Noun
Lightning | Electrify | Cloud | Charge | Verb

Request:

Obsidian | Harden | Stone
Forest | Breathe | Mist
Ember | Form | Lava
Dust | Scatter | Fire
Dew | Condense | Leaf

Your Response:

Obsidian | Harden | Stone | Blade | Noun
Forest | Breathe | Mist | Dew | Noun
Ember | Form | Lava | Glow | Verb
Dust | Scatter | Fire | Flicker | Verb
Dew | Condense | Leaf | Drip | Verb

Request:

Morning Sunlight | Casts a Glow | Dew-Covered Grass
Mountain Breeze | Carries Over | Valley Floor
Thunderstorm Clouds | Gather Above | Distant Horizon
Ocean Waves | Crash Against | Rocky Shore
Autumn Leaves | Drift Along | Forest Trail
Night Sky | Reflects Off | City Lights
Winter Chill | Settles Into | Quiet Lake

Your Response:

Morning Sunlight | Casts a Glow | Dew-Covered Grass | Warmth | Noun
Mountain Breeze | Carries Over | Valley Floor | Refresh | Verb
Thunderstorm Clouds | Gather Above | Distant Horizon | Darken | Verb
Ocean Waves | Crash Against | Rocky Shore | Foam | Noun
Autumn Leaves | Drift Along | Forest Trail | Blanket | Noun
Night Sky | Reflects Off | City Lights | Glimmer | Verb
Winter Chill | Settles Into | Quiet Lake | Freeze | Verb

Request:
";

    private const string UserPromptPostFix = @"

Your Response:
";
    
    public async Task<IEnumerable<MergeOutput>> PromptBatch(IEnumerable<MergeInput> input)
    {
        input = input.ToList();
        var userPrompt = GetPrompt(input);
        logger.LogInformation("Prompting with {Prompt}", userPrompt);
        var completionResult = await aiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem(SystemPrompt),
                ChatMessage.FromUser(userPrompt + UserPromptPostFix)
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
    
    private IEnumerable<MergeOutput> GetParsedResponse(string completionResponse)
    {
        return completionResponse.Split("\n").Select(x =>
        {
            var split = x.Split(" | ");
            var partOfSpeech = split[4].TrimEnd() switch
            {
                "Noun" => PartOfSpeech.Noun,
                "Verb" => PartOfSpeech.Verb,
                _ => throw new ArgumentOutOfRangeException(split[1])
            };
            return new MergeOutput(split[3], partOfSpeech);
        });
    }
    
    
}