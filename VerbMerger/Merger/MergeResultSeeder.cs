using VerbMerger.Merger.Persistence;

namespace VerbMerger.Merger;

public interface IMergeResultSeeder
{
    public IEnumerable<MergeResult> GetExemplarSeed();

    public HashSet<Word> GetAllValidWords()
    {
        // TODO: Cache This. As Static ideally, because seeder is injected as transient.
        return GetExemplarSeed()
            .SelectMany(x => x.Output.ToWords().Concat(x.Input.ToWords()))
            .ToHashSet();
    }
}

public class MergeResultSeeder : IMergeResultSeeder
{
    private string examples = @"
Water,Add,Fire,Steam,Noun
Fire,Add,Water,Extinguish,Verb
Stone,Chip,Obsidian,Blade,Noun
Fire,Harden,Stone,Obsidian,Noun
Air,Add,Mud,Splatter,Verb
Mountain,Crystallize,Mountain,Peak,Noun
Cliff,Char,Peak,Nonsense,Noun
Soil,Add,Ash,Fertilize,Verb
Water,Fertilize,Soil,Grow,Verb
Water,Fertilize,Air,Life,Noun
Life,Nourish,Life,Reproduce,Verb
Whirlwind,Add,Whirlwind,Chaos,Noun
Air,Add,Air,Whirlwind,Noun
Mud,Add,Fire,Harden,Verb
Chaos,Grow,Life,Evolve,Verb
Chaos,Add,Order,Chaos,Noun
Order,Add,Chaos,Knowledge,Noun
Knowledge,Reproduce,Knowledge,Book,Noun
Stone,Harden,Book,Tablet,Noun
Book,Crystalize,War,War and Peace,Noun
Book,Solidify,Fire,Fahrenheit 451,Noun
Book,Solidify,Water,Waterworld,Noun
Book,Crystalize,Fertility,Farmers Almanac,Noun
Book,Solidify,Air,Forecast,Noun
Knowledge,Fertilize,Life,Human,Noun
Human,Add,Book,Scribe,Noun
Mud,Remove,Water,Earth,Noun
Mist,Remove,Air,Condensation,Noun
Condensation,Remove,Water,Dust,Noun
Dust,Remove,Dirt,Nothing,Noun
Nothing,Add,Water,Water,Noun
";
    
    public IEnumerable<MergeResult> GetExemplarSeed()
    {
        return examples.Split('\n')
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(ParseExample);
    }
    
    private MergeResult ParseExample(string line)
    {
        var split = line.Split(",").Select(x => x.Trim()).ToArray();
        if (split.Length != 5)
        {
            throw new Exception($"Expected 5 parts, got {split.Length}");
        }
        
        var partOfSpeech = split[4] switch
        {
            "Noun" => PartOfSpeech.Noun,
            "Verb" => PartOfSpeech.Verb,
            _ => throw new ArgumentOutOfRangeException(split[4])
        };
        return new MergeResult(
            new MergeInput(split[0], split[1], split[2]),
            new MergeOutput(split[3], partOfSpeech)
        );
    }
}