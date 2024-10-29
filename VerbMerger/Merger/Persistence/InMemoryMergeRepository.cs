namespace VerbMerger.Merger.Persistence;

public class InMemoryMergeRepository : IMergeRepository, IMergeExampleSampler
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
}