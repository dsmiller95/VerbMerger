﻿using Microsoft.Extensions.Options;

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

    public MergerProompterBatchManager(
        IMergerBatchProompter proompter,
        IOptions<VerbMergerConfig> options,
        ILogger<BatchProompter> logger)
    {
        _proompter = proompter;
        _options = options;
        _pendingBatch = CreateNewBatch();
    }


    /// <summary>
    /// take all requests, process them, and set the resulting output in all of the cached requests.
    /// </summary>
    /// <param name="batch"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task ProcessBatch(List<PromptRequest> batch, CancellationToken cancellationToken)
    {
        var promptResult = await _proompter.PromptBatch(batch.Select(x => x.Input), cancellationToken);
        var promptList = promptResult.ToList();
        if(promptList.Count != batch.Count)
        {
            throw new Exception("Prompt batcher returned incorrect number of results");
        }
        
        for (var i = 0; i < batch.Count; i++)
        {
            batch[i].Output = promptList[i];
        }
    }

    private class PromptRequest(MergeInput input)
    {
        public MergeInput Input { get; } = input;
        public MergeOutput? Output { get; set; } = null;
    }


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
        return new PendingBatch(maxBatchSize, batchIntervalMs, _cancellationTokenSource.Token, ProcessBatch);
    }

    /// <summary>
    /// Thread-safe wrapper around a collection of requests and a completion source,
    /// representing a batch of requests that are ready to be processed.
    /// </summary>
    private class PendingBatch : IDisposable, IAsyncDisposable
    {
        private readonly int _maxBatchSize;
        private readonly int _batchIntervalMs;
        private readonly CancellationToken _cancellation;
        private readonly CancellationTokenRegistration _cancellationRegistration;
        private readonly Func<List<PromptRequest>, CancellationToken, Task> _performBatch;
        
        private List<PromptRequest> _requests = new();
        private TaskCompletionSource _batchCompletionSource = new();

        /// <summary>
        /// Thread-safe wrapper around a collection of requests and a completion source,
        /// representing a batch of requests that are ready to be processed.
        /// </summary>
        /// <param name="maxBatchSize"></param>
        /// <param name="batchIntervalMs"></param>
        /// <param name="cancellation"></param>
        /// <param name="performBatch"></param>
        public PendingBatch(int maxBatchSize, int batchIntervalMs, CancellationToken cancellation, Func<List<PromptRequest>, CancellationToken, Task> performBatch)
        {
            _maxBatchSize = maxBatchSize;
            _batchIntervalMs = batchIntervalMs;
            _cancellation = cancellation;
            _performBatch = performBatch;
            
            _cancellationRegistration = cancellation.Register(Cancelled);
        }

        private void Cancelled()
        {
            lock (this)
            {
                _batchCompletionSource.SetCanceled(_cancellation);
            }
        }

        private record BatchedRequest(List<PromptRequest> Requests, TaskCompletionSource CompletionSource);

        /// <summary>
        /// Takes all batch data out of the container, and replaces it with a new empty batch.
        /// protected by a lock.
        /// </summary>
        /// <returns></returns>
        private BatchedRequest TakeBatchedRequest()
        {
            var swapReqs = new List<PromptRequest>();
            var swapCompletion = new TaskCompletionSource();

            lock (this)
            {
                (swapReqs, _requests) = (_requests, swapReqs);
                (swapCompletion, _batchCompletionSource) = (_batchCompletionSource, swapCompletion);
            }
            return new BatchedRequest(swapReqs, swapCompletion);
        }
        
        public Task WaitForRequest(PromptRequest request)
        {
            Task completionTask;
            lock (this)
            {
                var isFirst = _requests.Count == 0;
                _requests.Add(request);
                var becameFull = _requests.Count == _maxBatchSize;
                if (isFirst) completionTask = BatchDelayMonitorAsync();
                else if (becameFull) completionTask = BatchCompleteAsync();
                else completionTask = _batchCompletionSource.Task;
            }
            return completionTask;
        }
        
        /// <summary>
        /// Wait for the maximum batch delay time, then process the batch, if it was not already processed.
        /// </summary>
        private async Task BatchDelayMonitorAsync()
        {
            var delayTask = Task.Delay(_batchIntervalMs, _cancellation);
            var completed = await Task.WhenAny(delayTask, _batchCompletionSource.Task);
            if (completed != delayTask) return; // batch was processed
            
            // batch has timed out, process it
            await BatchCompleteAsync();
        }

        /// <summary>
        /// Complete the batch by taking all requests out of the container and processing them.
        /// </summary>
        private async Task BatchCompleteAsync()
        {
            var takenBatch = TakeBatchedRequest();
            await _performBatch(takenBatch.Requests, _cancellation);
            takenBatch.CompletionSource.SetResult();
        }

        public void Dispose()
        {
            _cancellationRegistration.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await _cancellationRegistration.DisposeAsync();
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}