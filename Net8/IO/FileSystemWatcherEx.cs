using Com.H.Collections.Concurrent;
using Com.H.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
    
namespace Com.H.IO.InProgress
{
    internal record FileWatcherExInfo
    {
        private readonly object _lock = new();
        public FileSystemEventArgs Args;
        public FileInfo Info;
        private DateTime entryTime = DateTime.Now;
        public readonly AtomicGate Stable = new();
        public DateTime EntryTime
        {
            get
            {
                lock (this._lock)
                {
                    return this.entryTime;
                }
            }
        }

        public FileWatcherExInfo(FileSystemEventArgs args, FileInfo info)
        {
            this.Args = args;
            this.Info = info;
        }
        public void RefreshEntryTime()
        {
            lock (this._lock)
            {
                this.entryTime = DateTime.Now;
            }
        }
    }
    public class FileSystemWatcherEx : IDisposable
    {
        #region properties
        private readonly FileSystemWatcher _watcher;
        private bool disposedValue;
        private readonly LazyConcurrentDictionary<string, FileWatcherExInfo>
            _changedFiles = [];
        private readonly AtomicGate _started = new();
        private TaskCompletionSource taskCompletionSource = new();
        private TaskCompletionSource stoppingInProgress = new();
        private readonly object _lock = new();
        private bool SkipFileInUseCheck { get; set; } = false;
        public bool UseMd5ChecksumWhenDetectingFileChange { get; set; } = false;
        /// <summary>
        /// The interval in miliseconds to check whether or not the changed files
        /// have finished their file operation.
        /// </summary>
        public int FileChangeCheckInterval { get; set; } = 1000;
        public TimeSpan MaxWaitForFileChangeToComplete { get; set; } = TimeSpan.FromMilliseconds(1500);
        private CancellationTokenSource? _cts;

        #endregion

        #region constructors
        public FileSystemWatcherEx(string path, bool excludeSubFolders = false)
        {
            _watcher = new(path)
            {
                IncludeSubdirectories = !excludeSubFolders
            };
            stoppingInProgress.TrySetResult();
            _watcher.Created += Watcher_Created;
            _watcher.Deleted += Watcher_Deleted;
            _watcher.Renamed += Watcher_Renamed;
            _watcher.Changed += Watcher_Changed;
            _watcher.Error += Watcher_Error;
        }
        #endregion

        #region methods

        public async Task StartAsync(CancellationToken? cToken = null)
        {
            if (disposedValue || !this._started.TryOpen())
            {
                return;
            }
            // wait if there is a stoppage in progress
            await stoppingInProgress.Task;

            // 1) create a CancellationTokenSource that is linked to the cToken if cToken is not null.
            // 2) start a cancellable asynchronous task that checks changed files in the _changedFiles dictionary
            // that are still not finished being changed (e.g. , large files being overriden have the potential to generates multiple
            // change events.
            // 3) start the watcher
            // 4) await the task

            _cts =
                cToken.HasValue
                ? CancellationTokenSource.CreateLinkedTokenSource(cToken.Value)
                : new CancellationTokenSource();

            this._watcher.EnableRaisingEvents = true;
            lock (this._lock)
                this.taskCompletionSource = new();

            await CheckChangedCompletion(_cts.Token);
        }

        /// <summary>
        /// Check the enqueued files if they have finished their file operation. 
        /// E.g., large files being overriden have the potential to generates multiple 
        /// change events, however what this class is intended to do is invoking Changed event 
        /// only when those files finished being overriden.
        /// To do that, this CheckFileOperationCompletion method
        /// loops every 1000 miliseconds (configurable) through all 
        /// the queued files to check whether or not 
        /// those files have changed since the previous interval check based on 
        /// comparing their modified date, size, accessibility (free and not used / locked by another process)
        /// and their md5 checksum (optional). 
        /// And if no changes detected wait 1.5 seconds (configurable) 
        /// and do a final check on modified date & size to make sure that 
        /// no changes detected before invoking the Changed event.
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task CheckChangedCompletion(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // wait for the changed event to be raised
                try
                {
                    await this.taskCompletionSource.Task;
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                if (cancellationToken.IsCancellationRequested
                    || this.taskCompletionSource.Task.IsCanceled
                    )
                {
                    return;
                }
                // go through the changed files
                while (this._changedFiles.Count > 0 && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(this.FileChangeCheckInterval, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        return;
                    }
                    if (cancellationToken.IsCancellationRequested) return;
                    var keys = this._changedFiles.Keys.ToList();
                    foreach (var key in keys)
                    {
                        // check if cancellation is requested
                        if (cancellationToken.IsCancellationRequested) return;
                        if (!this._changedFiles.TryGetValue(key, out var value)
                            || value == null
                            )
                            continue;

                        // if the file has not been changed for more than 1.5 seconds (configurable), 
                        // then the file is not being changed anymore
                        if ((DateTime.Now - value.EntryTime) < this.MaxWaitForFileChangeToComplete)
                        {
                            continue;
                        }
                        // get the last modified date from file info,
                        // refresh file info,
                        // then compare the refreshed last modified date
                        // with the previous last modified date
                        // if the last modified date is different,
                        // then the file is still being changed
                        // update the entry time so that the file will be checked again
                        // after 1.5 seconds (configurable)
                        DateTime lastModified = value.Info.LastWriteTime;
                        value.Info.Refresh();
                        if (value.Info.LastWriteTime != lastModified)
                        {
                            // update the entry time & continue
                            // this forces another check on the file
                            // after 1.5 seconds (configurable)
                            value.RefreshEntryTime();
                            value.Stable.TryClose();
                            continue;
                        }

                        if (value.Stable.TryOpen())
                        {
                            // update the entry time one last time
                            // since the file is stable now
                            // this forces another check on the file
                            // after 1.5 seconds (configurable)
                            value.RefreshEntryTime();
                            continue;
                        }


                        // get the file size, refresh file info, then compare the refreshed file size
                        // with the previous file size
                        // if the file size is different, then the file is still being changed
                        // update the entry time so that the file will be checked again
                        // after 1.5 seconds (configurable)
                        long fileSize = value.Info.Length;
                        value.Info.Refresh();
                        if (value.Info.Length != fileSize)
                        {
                            // update the entry time
                            value.RefreshEntryTime();
                            continue;
                        }

                        // check if the file is accessible
                        // if the file is not accessible, then the file is still being changed
                        // update the entry time so that the file will be checked again
                        // after 1.5 seconds (configurable)
                        if (
                            !this.SkipFileInUseCheck
                            &&
                            value.Info.IsFileInUse())
                        {
                            // update the entry time
                            value.RefreshEntryTime();
                            continue;
                        }
                        //try
                        //{
                        //    using (var fs = File.Open(value.Info.FullName, FileMode.Open, FileAccess.Read, FileShare.None))
                        //    {
                        //        // update the entry time
                        //        value.RefreshEntryTime();
                        //        continue;
                        //    }
                        //}
                        //catch (IOException)
                        //{
                        //    // update the entry time
                        //    value.RefreshEntryTime();
                        //    continue;
                        //}

                        // invoke the Changed event and remove the file from the dictionary
                        this.Changed?.Invoke(this, value.Args);
                        this._changedFiles.TryRemove(key, out _);
                    }
                }
                lock (this._lock)
                {
                    this.taskCompletionSource = new();
                }
            }
        }
        public void Start(CancellationToken? cToken = null)
        {
            _ = this.StartAsync(cToken);
        }

        public void Stop()
        {
            if (!this._started.TryClose())
            {
                return;
            }
            this.stoppingInProgress = new();
            _watcher.EnableRaisingEvents = false;
            this.taskCompletionSource.TrySetCanceled();
            this._changedFiles.Clear();
            this._cts?.Cancel();
            this.stoppingInProgress.TrySetResult();
        }
        #endregion

        #region events
        private void Watcher_Error(object sender, ErrorEventArgs e)
        {
            this.Error?.Invoke(this, e);
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            lock (this._lock)
            {
                if (this.taskCompletionSource.Task.IsCanceled
                    || this._cts?.IsCancellationRequested == true
                    )
                {
                    return;
                }
                var added = this._changedFiles.TryAdd(e.FullPath, new FileWatcherExInfo(e, new FileInfo(e.FullPath)));
                if (added)
                {
                    this.taskCompletionSource.TrySetResult();
                }
            }
        }

        private void Watcher_Renamed(object sender, RenamedEventArgs e)
        {
            lock (this._lock)
            {
                if (this.taskCompletionSource.Task.IsCanceled
                    || this._cts?.IsCancellationRequested == true
                    )
                {
                    return;
                }
            }
            this.Renamed?.Invoke(this, e);


        }

        private void Watcher_Deleted(object sender, FileSystemEventArgs e)
        {
            lock (this._lock)
            {
                if (this.taskCompletionSource.Task.IsCanceled
                    || this._cts?.IsCancellationRequested == true
                    )
                {
                    return;
                }
            }
            this.Deleted?.Invoke(this, e);
        }

        private void Watcher_Created(object sender, FileSystemEventArgs e)
        {
            lock (this._lock)
            {
                if (this.taskCompletionSource.Task.IsCanceled
                    || this._cts?.IsCancellationRequested == true
                    )
                {
                    return;
                }
            }
            this.Created?.Invoke(this, e);
        }

        #endregion

        #region event handlers

        public event FileSystemEventHandler? Created;
        public event FileSystemEventHandler? Deleted;
        public event FileSystemEventHandler? Changed;
        public event RenamedEventHandler? Renamed;
        public event ErrorEventHandler? Error;

        #endregion

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        _watcher.Dispose();
                    }
                    catch { }
                    try
                    {
                        _changedFiles.Clear();
                    }
                    catch { }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~FileSystemListener()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion



    }

}
