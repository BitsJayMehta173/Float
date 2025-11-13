using MongoDB.Driver;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FloatingReminder
{
    public class SyncManager
    {
        private readonly string _username;
        private readonly Action _onRemoteChangeCallback;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _syncTask;

        /// <summary>
        /// Initializes a new SyncManager.
        /// </summary>
        /// <param name="username">The current user, to filter changes.</param>
        /// <param name="onRemoteChangeCallback">The function to call when a change is detected.</param>
        public SyncManager(string username, Action onRemoteChangeCallback)
        {
            _username = username;
            _onRemoteChangeCallback = onRemoteChangeCallback;
        }

        public void StartSync()
        {
            // Stop any previous sync task
            StopSync();

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            // Run the watcher on a background thread
            _syncTask = Task.Run(async () => await WatchForChanges(token), token);
        }

        public void StopSync()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _syncTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SyncManager] Error stopping sync: {ex.Message}");
            }
            _cancellationTokenSource = null;
            _syncTask = null;
        }

        private async Task WatchForChanges(CancellationToken token)
        {
            var collection = MongoSyncService.GetGlobalCollectionsDb();

            // We will watch the *entire* collection for any change
            // and let the MainWindow re-load its own data.
            // This is the simplest and most robust way.
            var options = new ChangeStreamOptions
            {
                FullDocument = ChangeStreamFullDocumentOption.UpdateLookup
            };

            Console.WriteLine($"[SyncManager] Starting to watch collection for user '{_username}'...");

            try
            {
                // collection.WatchAsync() returns a "cursor" that will
                // block and wait for new changes indefinitely.
                using (var cursor = await collection.WatchAsync(options, token))
                {
                    // ForEachAsync is a loop that waits for the next item
                    await cursor.ForEachAsync(change =>
                    {
                        if (token.IsCancellationRequested) return;

                        // A change was detected!
                        Console.WriteLine($"[SyncManager] Change detected: {change.OperationType}");

                        // Just invoke the callback.
                        // MainWindow will handle the UI update.
                        _onRemoteChangeCallback?.Invoke();

                    }, token);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[SyncManager] Sync stopped (cancellation requested).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SyncManager] Error in change stream: {ex.Message}");
            }

            Console.WriteLine("[SyncManager] Sync task ended.");
        }
    }
}