namespace VerbMerger.Merger;

public interface IMergePersistence
{
    public Task<MergeOutput?> GetPersistedOutput(MergeInput input);
    public Task PersistOutput(MergeInput input, MergeOutput output);
}

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
}