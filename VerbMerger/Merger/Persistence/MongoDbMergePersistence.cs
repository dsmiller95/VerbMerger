using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace VerbMerger.Merger.Persistence;

public class MongoDbMergePersistence : IMergeResultPersistence, IMergeExampleSampler
{
    private readonly IMongoCollection<DbModel> _collection;
    private readonly ILogger<MongoDbMergePersistence> _logger;

    public MongoDbMergePersistence(IMongoClient mongoClient, ILogger<MongoDbMergePersistence> logger)
    {
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

    public async Task<IEnumerable<MergeResult>> SampleExamples(int sampleCount)
    {
        var queryable = _collection.AsQueryable();

        var query = queryable
            .Sample(sampleCount)
            .Select(x => new MergeResult(x.Input, x.Output));

        return await query.ToListAsync();
    }

    public Task Initialize() => CreateIndexesAsync();

    private async Task CreateIndexesAsync()
    {
        var indexKeysDefinition = Builders<DbModel>.IndexKeys
            .Hashed(x => x.Input);
        var indexModel = new CreateIndexModel<DbModel>(indexKeysDefinition);
        await _collection.Indexes.CreateOneAsync(indexModel);
    }
    
    public async Task<MergeOutput?> GetPersistedOutput(MergeInput input)
    {
        var filter = Builders<DbModel>.Filter
            .Eq(x => x.Input, input);
        var findOptions = new FindOptions<DbModel, DbModel>
        {
            Limit = 1
        };
        var cursor = await _collection.FindAsync(filter, findOptions);
        var result = await cursor.FirstOrDefaultAsync();

        if (result == null) return null;
        
        _logger.LogInformation("Cache miss for {Input} resolved by database query.", input);
        return result.Output;
    }

    public async Task PersistOutput(MergeInput input, MergeOutput output)
    {
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
        if (updateResult.IsAcknowledged)
        {
            if (updateResult.ModifiedCount > 0) return;
            var didUpsert = updateResult.UpsertedId != null && updateResult.UpsertedId.BsonType != BsonType.Null;
            if (didUpsert) return;
            
            _logger.LogError("Failed to upsert merge result for {Input}. Inserting explicitly", input);
            await _collection.InsertOneAsync(new DbModel(input, output, currentMs));
        }
    }
}