using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;
using MongoDB.Driver;

namespace VerbMerger.Merger.Persistence;

public class MongoDbMergePersistence : IMergePersistence
{
    private readonly IMongoCollection<DbModel> _collection;
    private readonly IMemoryCache _memCache;
    private readonly ILogger<MongoDbMergePersistence> _logger;

    public MongoDbMergePersistence(IMongoClient mongoClient, IMemoryCache memCache, ILogger<MongoDbMergePersistence> logger)
    {
        _memCache = memCache;
        _logger = logger;

        const string dbName = "verb_merger";
        const string collectionName = "merge_results";
        var db = mongoClient.GetDatabase(dbName);
        _collection = db.GetCollection<DbModel>(collectionName);
    }

    private record DbModel(
        MergeInput Input,
        MergeOutput Output,
        long CreatedAtUnixMs)
    {
        public ObjectId Id { get; init; } = ObjectId.Empty;
    }
    public async Task<MergeOutput?> GetPersistedOutput(MergeInput input)
    {
        if(_memCache.TryGetValue(input, out MergeOutput? output))
        {
            return output;
        }
        
        _logger.LogInformation("Cache miss for {Input}. Querying database to fill cache.", input);
        var filter = Builders<DbModel>.Filter.Eq(x => x.Input, input);
        var cursor = await _collection.FindAsync(filter);
        var result = await cursor.FirstOrDefaultAsync();
        
        if(result != null) MemCacheOutput(input, result.Output);
        return result?.Output;
    }

    public async Task PersistOutput(MergeInput input, MergeOutput output)
    {
        var entryOptions = new MemoryCacheEntryOptions()
            .SetSize(input.Subject.Length + input.Verb.Length + input.Object.Length);
        _memCache.Set(input, output, entryOptions);
        
        
        var filter = Builders<DbModel>.Filter.Eq(x => x.Input, input);
        var currentMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var update = Builders<DbModel>.Update
            .Set(x => x.Output, output)
            .Set(x => x.CreatedAtUnixMs, currentMs);
        
        // update allows for updating an existing key, although at time of writing this does not occur.
        var updateOptions = new UpdateOptions()
        {
            IsUpsert = true
        };

        var updateResult = await _collection.UpdateOneAsync(filter, update, updateOptions);
        if (updateResult.IsAcknowledged && updateResult.UpsertedId.BsonType == BsonType.Null)
        {
            _logger.LogError("Failed to upsert merge result for {Input}. Inserting explicitly", input);
            await _collection.InsertOneAsync(new DbModel(input, output, currentMs));
        }
    }

    private void MemCacheOutput(MergeInput input, MergeOutput output)
    {
        var entryOptions = new MemoryCacheEntryOptions()
            .SetSize(input.Subject.Length + input.Verb.Length + input.Object.Length);
        _memCache.Set(input, output, entryOptions);
    }

    public async Task<IEnumerable<CacheDump>> DumpCache()
    {
        var cursor = await _collection.FindAsync(Builders<DbModel>.Filter.Empty);
        var results = await cursor.ToListAsync();
        return results.Select(x => new CacheDump(x.Input, x.Output));
    }
}