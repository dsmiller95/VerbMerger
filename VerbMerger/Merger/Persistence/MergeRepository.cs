using Microsoft.Extensions.Caching.Memory;

namespace VerbMerger.Merger.Persistence;

public class MergeRepository(
    IMergeResultPersistence mergePersistence,
    IMemoryCache memCache,
    ILogger<MergeRepository> logger)
    : IMergeRepository
{
    public async Task<MergeOutput?> FindOutput(MergeInput input)
    {
        if(memCache.TryGetValue(input, out MergeOutput? output))
        {
            return output;
        }
        
        var result = await mergePersistence.GetPersistedOutput(input);

        if (result == null) return null;
        
        logger.LogInformation("Cache miss for {Input} resolved by database query.", input);
        MemCacheOutput(input, result);
        return result;
    }

    public async Task SetOutput(MergeInput input, MergeOutput output)
    {
        MemCacheOutput(input, output);
        await mergePersistence.PersistOutput(input, output);
    }

    private void MemCacheOutput(MergeInput input, MergeOutput output)
    {
        var entryOptions = new MemoryCacheEntryOptions()
            .SetSize(input.Subject.Length + input.Verb.Length + input.Object.Length);
        memCache.Set(input, output, entryOptions);
    }
}