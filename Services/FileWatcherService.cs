using System;
using System.IO;

namespace OpenClaw.Windows.Services
{
    public class FileWatcherService : IDisposable
    {
        private FileSystemWatcher? _watcher;

        public event EventHandler<string>? FileDetected;

        public void StartWatching(string path)
        {
            if (!Directory.Exists(path)) return;

            _watcher = new FileSystemWatcher(path);
            _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            _watcher.Created += OnCreated;
            _watcher.EnableRaisingEvents = true;
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            // Simple filter: Ignore temporary or hidden files (null check)
            if (string.IsNullOrEmpty(e.Name) || e.Name.StartsWith("~$") || e.Name.StartsWith(".")) return;

            FileDetected?.Invoke(this, e.FullPath);
        }

        public void Dispose()
        {
            _watcher?.Dispose();
        }
    }
}
