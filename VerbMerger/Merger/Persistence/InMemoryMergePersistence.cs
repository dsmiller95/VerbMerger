namespace VerbMerger.Merger.Persistence;

public class InMemoryMergePersistence : IMergePersistence
{
    private readonly Dictionary<MergeInput, MergeOutput> _cache = new();

    public Task<MergeOutput?> GetPersistedOutput(MergeInput input)
    {
        if (_cache.TryGetValue(input, out var output))
        {
            return Task.FromResult<MergeOutput?>(output);
        }

        return Task.FromResult<MergeOutput?>(null);
    }

    public Task PersistOutput(MergeInput input, MergeOutput output)
    {
        _cache[input] = output;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<CacheDump>> DumpCache()
    {
        return Task.FromResult(_cache.Select(x => new CacheDump(x.Key, x.Value)));
    }
}