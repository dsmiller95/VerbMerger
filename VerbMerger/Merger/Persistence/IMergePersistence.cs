namespace VerbMerger.Merger.Persistence;


public interface IMergeExampleSampler
{
    /// <summary>
    /// Samples representative or random examples from the merge results.
    /// </summary>
    public Task<IEnumerable<MergeResult>> SampleExamples(int exampleCount);
}

/// <summary>
/// Persists merge results into a persistent store, such as a database, or the filesystem.
/// </summary>
public interface IMergeResultPersistence
{
    public Task<MergeOutput?> GetPersistedOutput(MergeInput input);
    public Task PersistOutput(MergeInput input, MergeOutput output);
    
    /// <summary>
    /// any startup initialization. for example, creation of indexes or applying database migrations. 
    /// </summary>
    /// <returns></returns>
    public Task Initialize();
}

/// <summary>
/// Gets and sets merge results. may not be persistent, for EX an in-memory cache.
/// </summary>
public interface IMergeRepository
{
    public Task<MergeOutput?> FindOutput(MergeInput input);
    public Task SetOutput(MergeInput input, MergeOutput output);
}