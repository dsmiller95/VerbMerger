namespace VerbMerger.Merger.Persistence;

public interface IMergePersistence
{
    public Task<MergeOutput?> GetPersistedOutput(MergeInput input);
    public Task PersistOutput(MergeInput input, MergeOutput output);
    public Task<IEnumerable<CacheDump>> DumpCache();
    
    /// <summary>
    /// any startup initialization. for example, creation of indexes or applying database migrations. 
    /// </summary>
    /// <returns></returns>
    public Task Initialize();
}