namespace VerbMerger.Merger.Persistence;

public class InMemoryMergeRepository : IMergeRepository, IMergeSampler
{
    private readonly Dictionary<MergeInput, MergeOutput> _cache = new();

    public Task<MergeOutput?> FindOutput(MergeInput input)
    {
        if (_cache.TryGetValue(input, out var output))
        {
            return Task.FromResult<MergeOutput?>(output);
        }

        return Task.FromResult<MergeOutput?>(null);
    }

    public Task SetOutput(MergeInput input, MergeOutput output)
    {
        _cache[input] = output;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<MergeResult>> SampleExamples(int exampleCount)
    {
        return Task.FromResult(_cache.Take(exampleCount).Select(x => new MergeResult(x.Key, x.Value)));
    }

    [Flags]
    private enum PartialFilterStatus
    {
        SubjectPresent = 1<<0,
        VerbPresent = 1<<1,
        ObjectPresent = 1<<2,
        
        NonePresent = 0,
        AllPresent = SubjectPresent | VerbPresent | ObjectPresent
    }

    public Task<IEnumerable<MergeFilterResult>> Filter(IEnumerable<MergeInput> inputs)
    {
        var inputArr = inputs.ToArray();
        var results = new PartialFilterStatus[inputArr.Length];
        
        var wordToInputIndexMap = new Dictionary<Word, List<int>>();
        for (int i = 0; i < inputArr.Length; i++)
        {
            var input = inputArr[i];
            foreach (var word in input.ToWords())
            {
                AddToMap(wordToInputIndexMap, word, i);
            }
        }

        var validWords = _cache
            .SelectMany(x => x.Key.ToWords().Concat(x.Value.ToWords()));

        foreach (Word validWord in validWords)
        {
            var indicesToCheck = wordToInputIndexMap.GetValueOrDefault(validWord);
            if (indicesToCheck == null) continue;
            
            foreach (var index in indicesToCheck)
            {
                var addFilterStatus = PartialFilterStatus.NonePresent;
                switch (validWord.PartOfSpeech)
                {
                    case PartOfSpeech.Verb:
                        addFilterStatus = PartialFilterStatus.VerbPresent;
                        break;
                    case PartOfSpeech.Noun:
                        var atIndex = inputArr[index];
                        if(validWord.Text == atIndex.Subject) addFilterStatus = PartialFilterStatus.SubjectPresent;
                        else if(validWord.Text == atIndex.Object) addFilterStatus = PartialFilterStatus.ObjectPresent;
                        else throw new Exception($"Invalid match. Matched on word {validWord} for input {atIndex}, but did not match subject or object.");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
                results[index] |= addFilterStatus;
            }
        }
        
        return Task.FromResult(results.Select((x, i) => new MergeFilterResult(inputArr[i], x switch
        {
            PartialFilterStatus.AllPresent => FilterStatus.Valid,
            _ => FilterStatus.TermMissing
        })));
    }

    
    private static void AddToMap<TKey, TMap>(Dictionary<TKey, List<TMap>> map, TKey key, TMap value)
        where TKey : notnull
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }

        list.Add(value);
    }
}