using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace VerbMerger.Merger.Persistence;

public class MongoDbMergePersistence : IMergeResultPersistence, IMergeSampler
{
    private readonly IMongoCollection<DbModel> _collection;
    private readonly ILogger<MongoDbMergePersistence> _logger;
    private readonly IMergeResultSeeder _seeder;

    public MongoDbMergePersistence(IMongoClient mongoClient, ILogger<MongoDbMergePersistence> logger, IMergeResultSeeder seeder)
    {
        _logger = logger;
        _seeder = seeder;

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
        public bool IsExemplar { get; init; } = false;
    }

    public async Task<IEnumerable<MergeResult>> SampleExamples(int sampleCount)
    {
        var queryable = _collection.AsQueryable();

        var query = queryable
            .Where(x => x.IsExemplar)
            .Sample(sampleCount)
            .Select(x => new MergeResult(x.Input, x.Output));

        return await query.ToListAsync();
    }

    public async Task<IEnumerable<MergeFilterResult>> Filter(IEnumerable<MergeInput> inputs)
    {
        var allValidFromSeed = this._seeder.GetAllValidWords();

        var mergeInputs = inputs.ToList();
        
        var notFoundWords = mergeInputs
            .SelectMany(x => x.ToWords())
            .Where(x => !allValidFromSeed.Contains(x))
            .Select(x => x.ToMergeOutput())
            .ToHashSet();
        if (notFoundWords.Count > 100)
        {
            _logger.LogWarning("Filtering too many words, may negatively affect query performance. {WordCount}", notFoundWords.Count);
        }
        
        if(notFoundWords.Count <= 0)
        {
            return mergeInputs.Select(x => new MergeFilterResult(x, FilterStatus.Valid));
        }
        
        
        var filter = Builders<DbModel>.Filter
            .In(x => x.Output, notFoundWords.ToList());
        
        var pipeline = new EmptyPipelineDefinition<DbModel>()
            .Match(filter)
            .Group(x => x.Output, g => new
            {
                _id = g.Key
            })
            ;

        var results = await _collection.AggregateAsync(pipeline);
        var foundWords = await results.ToListAsync();

        notFoundWords.ExceptWith(foundWords.Select(x => x._id));

        var result = mergeInputs.Select(x =>
        {
            FilterStatus status = FilterStatus.Valid;
            var allWords = x.ToWords().Select(w => w.ToMergeOutput());
            if (allWords.Any(w => notFoundWords.Contains(w)))
            {
                // if any words in the input are not found:
                status = FilterStatus.TermMissing;
            }

            return new MergeFilterResult(x, status);
        });
        
        return result;
    }

    public async Task Initialize()
    {
        try
        {
            await CreateIndexesAsync();
            await SeedExemplarData();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to initialize persistence");
            throw;
        }
    }

    private async Task CreateIndexesAsync()
    {
        var indexes = new CreateIndexModel<DbModel>[]
        {
            new(Builders<DbModel>.IndexKeys.Hashed(x => x.Input), new CreateIndexOptions
            {
            }),
            new(Builders<DbModel>.IndexKeys.Ascending(x => x.Input), new CreateIndexOptions
            {
                Unique = true
            })
        };
        
        await _collection.Indexes.CreateManyAsync(indexes);
    }
    
    private async Task SeedExemplarData()
    {
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var seedData = _seeder.GetExemplarSeed().Select(
            x => new DbModel(x.Input, x.Output, currentTime)
        {
            IsExemplar = true
        });
        await BulkUpsert(seedData);
    }

    private async Task BulkUpsert(IEnumerable<DbModel> models)
    {
        var bulkOps = models
            .Select(item =>
            {
                var filter = Builders<DbModel>.Filter.Eq(x => x.Input, item.Input);
                var update = Builders<DbModel>.Update
                        .Set(x => x.Output, item.Output)
                        .Set(x => x.CreatedAtUnixMs, item.CreatedAtUnixMs)
                        .Set(x => x.IsExemplar, item.IsExemplar)
                    ;
                return new UpdateOneModel<DbModel>(filter, update) { IsUpsert = true };
            });
        
        await _collection.BulkWriteAsync(bulkOps);
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