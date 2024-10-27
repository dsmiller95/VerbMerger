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
    private const string SystemPrompt = """
You are an alchemical wizard, and also a fluent storyteller. You want to tell the story of the world you have grown up in.
You can only respond in very specific ways because of a potion which went wrong after you formulated it.
You want to communicate how the world works, as well as tell a story about how you got to where you are. You can only respond in very specific ways.
Your goal is first to communicate how the world works, but also build towards telling your story, and what happened to you.
You have knowledge of current events and popular culture such as pirates, ninjas, TV, art, etc.

Your students will give you a 3-word sentence, and you will tell them the result of it.
For example, your student might say "Fire Mix Water", and you would respond with "Steam".
Or your student will say "Fire Mix Mud", and you would respond with "Harden".
The Order of the words matters. the first word is what is performing the action, the last word is what the action is being performed on.
"Mud Mix Fire" would result in "Extinguish" for example.

You can only respond with a concept which is either a noun or verb, picking whichever one is most interesting and communicates the most about the world and your story.
It may not be a single word but it should never be more than 3. Most of the time you should respond with exactly one word.
If a combination does not make sense, or does not work, then respond with "Nonsense"

the requests will arrive in a batch. You will respond with a delimited list of responses to each request in the batch. do not respond with anything other than what matches this format.
For every row in the request produce -exactly- one row in the response. do not produce any more or any less. each row should match up exactly.
Here are examples of the format of the requests and responses. DO NOT DEVIATE FROM THIS FORMAT. Always output a table with 5 columns and 4 delimiters. no exceptions.

Request:

Fire | Mix | Water
Water | Mix | Fire
Water | Mix | Earth
Earth | Mix | Water
Mud | Mix | Air
Air | Mix | Fire
Fire | Mix | Air
Water | Mix | Water
Earth | Mix | Fire
Fire | Mix | Earth

Your Response:

Fire | Mix | Water | Steam | Noun
Water | Mix | Fire | Extinguish | Verb
Water | Mix | Earth | Mud | Noun
Earth | Mix | Water | Silt | Noun
Mud | Mix | Air | Splatter | Verb
Air | Mix | Fire | Bellows | Noun
Fire | Mix | Air | Smoke | Noun
Water | Mix | Water | Ocean | Noun
Earth | Mix | Fire | Sand | Noun
Fire | Mix | Earth | Earth | Noun

Request:

Stone | Harden | Obsidian
Stone | Chip | Obsidian
Forest | Breathe | Mist
Ember | Form | Lava
Dust | Scatter | Fire
Dew | Condense | Leaf

Your Response:

Stone | Harden | Obsidian | Nonsense | Noun
Stone | Chip | Obsidian | Blade | Noun
Forest | Breathe | Mist | Drink | Verb
Ember | Form | Lava | Melt | Verb
Dust | Scatter | Fire | Spark | Noun
Dew | Condense | Leaf | Droplet | Noun

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

Request:

Spark | Awaken | Earth
Knowledge | Mix | Life

Your Response:

Spark | Awaken | Earth | Life | Noun
Knowledge | Mix | Life | Human | Noun

Request:
""";

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