using System.IO;

namespace Pixora.Services;

public enum FolderChangeKind
{
    Created,
    Deleted,
    Changed,
    Renamed,
}

public sealed record FolderChange(FolderChangeKind Kind, string Path, string? OldPath = null);

public sealed record FolderChangeBatch(IReadOnlyList<FolderChange> Changes, bool RequiresFullRefresh = false);

public sealed class FolderChangeMonitor : IDisposable
{
    private readonly object _sync = new();
    private readonly TimeSpan _debounceDelay;
    private readonly List<FolderChange> _pendingChanges = [];
    private FileSystemWatcher? _watcher;
    private Timer? _timer;
    private bool _requiresFullRefresh;
    private bool _disposed;

    public FolderChangeMonitor(TimeSpan? debounceDelay = null)
    {
        _debounceDelay = debounceDelay ?? TimeSpan.FromMilliseconds(700);
    }

    public event EventHandler<FolderChangeBatch>? BatchReady;

    public string? Folder { get; private set; }

    public void Start(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ObjectDisposedException.ThrowIf(_disposed, this);
        Stop();

        var fullFolder = Path.GetFullPath(folder);
        var watcher = new FileSystemWatcher(fullFolder)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size
                | NotifyFilters.CreationTime,
        };
        watcher.Created += Watcher_Created;
        watcher.Deleted += Watcher_Deleted;
        watcher.Changed += Watcher_Changed;
        watcher.Renamed += Watcher_Renamed;
        watcher.Error += Watcher_Error;

        lock (_sync)
        {
            Folder = fullFolder;
            _watcher = watcher;
            _timer = new Timer(FlushPendingChanges);
            watcher.EnableRaisingEvents = true;
        }
    }

    public void Stop()
    {
        FileSystemWatcher? watcher;
        Timer? timer;
        lock (_sync)
        {
            watcher = _watcher;
            timer = _timer;
            _watcher = null;
            _timer = null;
            Folder = null;
            _pendingChanges.Clear();
            _requiresFullRefresh = false;
        }

        timer?.Dispose();
        if (watcher is null)
        {
            return;
        }

        try
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= Watcher_Created;
            watcher.Deleted -= Watcher_Deleted;
            watcher.Changed -= Watcher_Changed;
            watcher.Renamed -= Watcher_Renamed;
            watcher.Error -= Watcher_Error;
            watcher.Dispose();
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }

    private void Watcher_Created(object sender, FileSystemEventArgs e)
    {
        Queue(new FolderChange(FolderChangeKind.Created, e.FullPath));
    }

    private void Watcher_Deleted(object sender, FileSystemEventArgs e)
    {
        Queue(new FolderChange(FolderChangeKind.Deleted, e.FullPath));
    }

    private void Watcher_Changed(object sender, FileSystemEventArgs e)
    {
        Queue(new FolderChange(FolderChangeKind.Changed, e.FullPath));
    }

    private void Watcher_Renamed(object sender, RenamedEventArgs e)
    {
        Queue(new FolderChange(FolderChangeKind.Renamed, e.FullPath, e.OldFullPath));
    }

    private void Watcher_Error(object sender, ErrorEventArgs e)
    {
        lock (_sync)
        {
            if (_disposed || _timer is null)
            {
                return;
            }

            _requiresFullRefresh = true;
            _timer.Change(_debounceDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void Queue(FolderChange change)
    {
        lock (_sync)
        {
            if (_disposed || _timer is null)
            {
                return;
            }

            _pendingChanges.Add(change);
            _timer.Change(_debounceDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void FlushPendingChanges(object? state)
    {
        FolderChangeBatch batch;
        lock (_sync)
        {
            if (_disposed || (_pendingChanges.Count == 0 && !_requiresFullRefresh))
            {
                return;
            }

            batch = new FolderChangeBatch(_pendingChanges.ToArray(), _requiresFullRefresh);
            _pendingChanges.Clear();
            _requiresFullRefresh = false;
        }

        BatchReady?.Invoke(this, batch);
    }
}
