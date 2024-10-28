using Microsoft.Extensions.Options;

namespace VerbMerger.Merger;

public interface IMergerProompter
{
    public Task<MergeOutput> Prompt(MergeInput input);
}

public class MergerProompterBatchManager : IMergerProompter, IDisposable
{
    private readonly IMergerBatchProompter _proompter;
    private readonly IOptions<VerbMergerConfig> _options;
    private readonly PendingBatch _pendingBatch;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    private readonly Task _batchProcessingTask;
    
    private readonly List<Task> _pendingBatchTasks = new();
    

    public MergerProompterBatchManager(
        IMergerBatchProompter proompter,
        IOptions<VerbMergerConfig> options,
        ILogger<BatchProompter> logger)
    {
        _proompter = proompter;
        _options = options;
        _pendingBatch = CreateNewBatch();
        _batchProcessingTask = Task.Run(() => ProcessBatches(_cancellationTokenSource.Token));
    }

    private async Task ProcessBatches(CancellationToken cancellationToken)
    {
        var delayTask = Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            var takenBatch = _pendingBatch.TakeBatchedRequestIfDue();
            if (takenBatch != null)
            {
                var processingBatch = Task.Run(async () =>
                {
                    await ProcessBatch(takenBatch, cancellationToken);
                }, cancellationToken);
                
                _pendingBatchTasks.RemoveAll(x => x.IsCompleted);
                _pendingBatchTasks.Add(processingBatch);
            }
            await delayTask;
        }
    }

    /// <summary>
    /// take all requests, process them, and set the resulting output in all of the cached requests.
    /// </summary>
    /// <param name="batch"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task ProcessBatch(BatchedRequest batch, CancellationToken cancellationToken)
    {
        var promptResult = await _proompter.PromptBatch(batch.Requests.Select(x => x.Input));
        var promptList = promptResult.ToList();
        if(promptList.Count != batch.Requests.Count)
        {
            throw new Exception("Prompt batcher returned incorrect number of results");
        }
        
        for (var i = 0; i < batch.Requests.Count; i++)
        {
            batch.Requests[i].Output = promptList[i];
        }
        
        batch.CompletionSource.SetResult();
    }

    private class PromptRequest(MergeInput input)
    {
        public MergeInput Input { get; } = input;
        public MergeOutput? Output { get; set; } = null;
    }

    private record BatchedRequest(List<PromptRequest> Requests, TaskCompletionSource CompletionSource);


    public async Task<MergeOutput> Prompt(MergeInput input)
    {
        var request = new PromptRequest(input);
        
        await _pendingBatch.WaitForRequest(request);
        if(request.Output == null) throw new Exception("Prompt batcher failed to get output");
        return request.Output;
    }
    
    private PendingBatch CreateNewBatch()
    {
        var maxBatchSize = _options.Value.PromptMaxBatchSize;
        var batchIntervalMs = _options.Value.PromptBatchIntervalMs;
        return new PendingBatch(maxBatchSize, batchIntervalMs);
    }
    
    /// <summary>
    /// Thread-safe wrapper around a collection of requests and a completion source,
    /// representing a batch of requests that are ready to be processed.
    /// </summary>
    /// <param name="maxBatchSize"></param>
    /// <param name="batchIntervalMs"></param>
    private class PendingBatch(int maxBatchSize, int batchIntervalMs)
    {
        private List<PromptRequest> _requests = new();
        private TaskCompletionSource _batchCompletionSource = new();
        private long _batchStartTimeMs = -1;

        public BatchedRequest? TakeBatchedRequestIfDue()
        {
            if(_batchStartTimeMs == -1) return null;

            if (!(IsBatchExpired() || IsBatchFull()))
            {
                return null;
            }
            
            var swapReqs = new List<PromptRequest>();
            var swapCompletion = new TaskCompletionSource();

            lock (this)
            {
                (swapReqs, _requests) = (_requests, swapReqs);
                (swapCompletion, _batchCompletionSource) = (_batchCompletionSource, swapCompletion);
                _batchStartTimeMs = -1;
            }
            return new BatchedRequest(swapReqs, swapCompletion);
        }
        
        
        public Task WaitForRequest(PromptRequest request)
        {
            EnsureBatchStarted();
            Task completionTask;
            lock (this)
            {
                _requests.Add(request);
                completionTask = _batchCompletionSource.Task;
            }
            return completionTask;
        }
        
        private bool IsBatchExpired()
        {
            if(_batchStartTimeMs == -1) return false;
            var timeSinceBatchStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _batchStartTimeMs;
            return timeSinceBatchStart >= batchIntervalMs;
        }
        
        private bool IsBatchFull()
        {
            return _requests.Count >= maxBatchSize;
        }

        private void EnsureBatchStarted()
        {
            if(_batchStartTimeMs != -1) return;
            _batchStartTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _batchProcessingTask.Wait();
        _cancellationTokenSource.Dispose();
    }
}