using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;

namespace Journalist
{
    class Packer : IDisposable
    {
        protected FileSystemWatcher watcher;
        protected string WatchingPath;
        protected string targetFileNameFilter;
        protected IList<string> packFileNameFilters;

        private const int packItemLimit = 100;
        protected HashSet<string> packFileNames = new HashSet<string>();
        public bool PackFull { get => packFileNames.Count >= packItemLimit; }

        protected Thread updatingThread;
        public bool UpdatingPackFiles { get; protected set; }
        public event Action PackUpdateFinishing;
        public event Action PackUpdateFinished;

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

        // can be used without disposing
        public Packer ChangeConfig(Config config)
        {

            WatchingPath = config.Path ?? @".\";
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

            targetFileNameFilter = config.TargetFileNameFilter ?? "";
            watcher = new FileSystemWatcher(WatchingPath, targetFileNameFilter)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
            };
            watcher.Changed += HandleFileEvent;
            watcher.Created += HandleFileEvent;
            watcher.Deleted += HandleFileEvent;
            watcher.Renamed += HandleFileEvent;
            watcher.EnableRaisingEvents = true;

            return this;
        }

        public void Dispose()
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
                    queue.Enqueue(WatchingPath);
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
                    PackUpdateFinishing?.Invoke();
                    UpdatingPackFiles = false;
                }
                PackUpdateFinished?.Invoke();
            }

            if (updatingThread == null || updatingThread.ThreadState == System.Threading.ThreadState.Stopped)
            {
                updatingThread = new Thread(new ThreadStart(update));
                updatingThread.Start();
            }
        }

        private void HandleFileEvent(object sender, FileSystemEventArgs e)
        {
            StartUpdatingPackFiles();
        }

        // returns zip file name, returned file should be deleted after use
        public string Pack()
        {
            string packingDir;
            bool copied = PackFull;  // cache to avoid mis-deleting
            if (copied)
            {
                packingDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "pack")).FullName;
                lock (packFileNames)
                {
                    foreach (var path in packFileNames)
                    {
                        string relativePath;
                        try
                        {
                            relativePath = GetRelativePath(WatchingPath, path);
                        }
                        catch (Exception e)
                        {
                            relativePath = Path.GetFileName(path);
                            Console.WriteLine($"Error: Following exception occurred while getting relative path.\n{e}");
                        }
                        File.Copy(path, Path.Combine(packingDir, relativePath), true);
                    }
                }
            }
            else
            {
                packingDir = new DirectoryInfo(WatchingPath).FullName;
            }
            string zipExe = Path.Combine(
                Path.GetDirectoryName(Application.ResourceAssembly.Location),
                "7z.exe");
            string zipFileName = Path.Combine(Path.GetTempPath(), $"{DateTime.Now.Ticks}.zip");

            var filterList = new string[packFileNameFilters.Count + 1];
            filterList[0] = targetFileNameFilter;
            packFileNameFilters.CopyTo(filterList, 1);
            string filterListString = string.Join(" ", filterList);
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = zipExe,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = $"a {zipFileName} {filterListString} -r",
                UseShellExecute = false,
                WorkingDirectory = packingDir,
                RedirectStandardOutput = true
            };
            using (Process process = Process.Start(psi))
            {
                process.WaitForExit();
            }
            if (copied)
            {
                Directory.Delete(packingDir);
            }
            return zipFileName;
        }

        private static string GetRelativePath(string fromPath, string toPath)
        {
            int fromAttr = GetPathAttribute(fromPath);
            int toAttr = GetPathAttribute(toPath);

            StringBuilder path = new StringBuilder(260); // MAX_PATH
            if (PathRelativePathTo(
                path,
                fromPath,
                fromAttr,
                toPath,
                toAttr) == 0)
            {
                throw new ArgumentException("Paths must have a common prefix");
            }
            return path.ToString();
        }

        private static int GetPathAttribute(string path)
        {
            DirectoryInfo di = new DirectoryInfo(path);
            if (di.Exists)
            {
                return FILE_ATTRIBUTE_DIRECTORY;
            }

            FileInfo fi = new FileInfo(path);
            if (fi.Exists)
            {
                return FILE_ATTRIBUTE_NORMAL;
            }

            throw new FileNotFoundException();
        }

        private const int FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const int FILE_ATTRIBUTE_NORMAL = 0x80;

        [DllImport("shlwapi.dll", SetLastError = true)]
        private static extern int PathRelativePathTo(StringBuilder pszPath,
            string pszFrom, int dwAttrFrom, string pszTo, int dwAttrTo);
    }
}
