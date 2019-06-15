using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Journalist
{
    class Packer
    {
        protected FileSystemWatcher watcher;
        protected string Path;
        protected IList<string> packFileNameFilters;

        private const int packItemLimit = 100;
        protected HashSet<string> packFileNames = new HashSet<string>();
        public bool PackFull { get => packFileNames.Count >= packItemLimit; }

        protected Thread updatingThread;
        public bool UpdatingPackFiles { get; protected set; }
        public event Action PackUpdateFinishing;

        public class Config
        {
            public string Path { get; set; }

            // Target file to watch. Changes of pack files are logged only if target file has been changed.
            public string TargetFileNameFilter { get; set; }

            // All included files will be uploaded.
            public IList<string> PackFileNameFilters { get; set; }
        }

        public Packer(Config config)
        {
            ChangeConfig(config);
        }

        public void ChangeConfig(Config config)
        {

            Path = config.Path ?? @".\";
            packFileNameFilters = config.PackFileNameFilters ?? new List<string>();

            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= HandleFileEvent;
                watcher.Created -= HandleFileEvent;
                watcher.Deleted -= HandleFileEvent;
                watcher.Renamed -= HandleFileEvent;
            }
            updatingThread?.Abort();

            watcher = new FileSystemWatcher(Path, config.TargetFileNameFilter ?? "")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
            };
            watcher.Changed += HandleFileEvent;
            watcher.Created += HandleFileEvent;
            watcher.Deleted += HandleFileEvent;
            watcher.Renamed += HandleFileEvent;
            watcher.EnableRaisingEvents = true;
        }

        ~Packer()
        {
            updatingThread?.Join(1000);
            updatingThread?.Abort();
        }

        public void StartUpdatingPackFiles()
        {
            void update()
            {
                lock (packFileNames)
                {
                    UpdatingPackFiles = true;
                    packFileNames.Clear();

                    var queue = new Queue<string>();
                    queue.Enqueue(Path);
                    while (queue.Count > 0 && !PackFull)
                    {
                        var dir = queue.Dequeue();

                        foreach (var sub in Directory.GetDirectories(dir))
                        {
                            queue.Enqueue(sub);
                        }

                        foreach (var pattern in packFileNameFilters)
                        {
                            if (!PackFull)
                            {
                                var files = Directory.GetFiles(dir, pattern)
                                    .Take(packItemLimit - packFileNames.Count)
                                    .ToList();
                                packFileNames.UnionWith(files);
                            }
                            else
                            {
                                Console.WriteLine($"WARNING: Files exceeding limit ({packItemLimit}).");
                                break;
                            }
                        }
                    }
                    PackUpdateFinishing();
                    UpdatingPackFiles = false;
                }
            }

            if (updatingThread == null || updatingThread.ThreadState == ThreadState.Stopped)
            {
                updatingThread = new Thread(new ThreadStart(update));
                updatingThread.Start();
            }
        }

        private void HandleFileEvent(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"change type: {e.ChangeType} fullpath: {e.FullPath}");
        }
    }
}
