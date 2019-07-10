using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace Journalist
{
    class Packer
    {
        protected FileSystemWatcher watcher;
        protected string WatchingPath;
        protected string targetFileNameFilter;
        protected IList<string> packFileNameFilters;
        protected IList<string> excludedPaths;

        private const int packItemLimit = 100;

        public class FileItem
        {
            public string FileName { get; set; }
            public long Length { get; set; }
            public DateTime Creation { get; set; }
            public DateTime LastWrite { get; set; }
            public FileItem(string fileName)
            {
                FileName = fileName;
                Length = new FileInfo(fileName).Length;
                Creation = File.GetCreationTime(fileName);
                LastWrite = File.GetLastWriteTime(fileName);
            }
        }

        public ObservableCollection<FileItem> PackFileNames = new ObservableCollection<FileItem>();
        public bool PackFull { get => PackFileNames.Count >= packItemLimit; }

        public bool UpdatingPackFiles { get; protected set; }
        public event Action PackUpdateFinished;

        public class Config
        {
            public string Path { get; set; }

            // Target file to watch. Changes of pack files are logged only if target file has been changed.
            public string TargetFileNameFilter { get; set; }

            // All included files will be uploaded.
            public IList<string> PackFileNameFilters { get; set; }

            public IList<string> ExcludedPaths { get; set; }
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
            excludedPaths = config.ExcludedPaths ?? new List<string>();

            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= HandleFileEvent;
                watcher.Created -= HandleFileEvent;
                watcher.Deleted -= HandleFileEvent;
                watcher.Renamed -= HandleFileEvent;
            }

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

        public void StartUpdatingPackFiles()
        {
            void update()
            {
                var filters = packFileNameFilters.ToList();
                filters.Add(targetFileNameFilter);

                var excludedFullName = new List<string>(excludedPaths.Count);
                foreach (var item in excludedPaths)
                {
                    try
                    {
                        excludedFullName.Add(new DirectoryInfo(item).FullName);
                    }
                    catch (FileNotFoundException)
                    {
                        continue;
                    }
                }

                if (UpdatingPackFiles)
                {
                    return;
                }
                UpdatingPackFiles = true;
                lock (PackFileNames)
                {
                    PackFileNames.Clear();

                    var queue = new Queue<string>();
                    queue.Enqueue(WatchingPath);
                    while (queue.Count > 0 && !PackFull)
                    {
                        var dir = queue.Dequeue();

                        foreach (var sub in Directory.GetDirectories(dir))
                        {
                            if (!excludedFullName.Contains(new DirectoryInfo(sub).FullName))
                            {
                                queue.Enqueue(sub);
                            }
                        }

                        foreach (var pattern in filters)
                        {
                            if (!PackFull)
                            {
                                var files = Directory.GetFiles(dir, pattern)
                                    .Take(packItemLimit - PackFileNames.Count)
                                    .ToList();
                                foreach (var file in files)
                                {
                                    PackFileNames.Add(new FileItem(file));
                                }
                            }
                            else
                            {
                                Console.WriteLine($"WARNING: Files exceeding limit ({packItemLimit}).");
                                break;
                            }
                        }
                    }
                }
                UpdatingPackFiles = false;
                PackUpdateFinished?.Invoke();
            }

            Application.Current.Dispatcher.BeginInvoke(new Action(() => update()));
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
                lock (PackFileNames)
                {
                    foreach (var fileItem in PackFileNames)
                    {
                        string relativePath;
                        try
                        {
                            relativePath = GetRelativePath(WatchingPath, fileItem.FileName);
                        }
                        catch (Exception e)
                        {
                            relativePath = Path.GetFileName(fileItem.FileName);
                            Console.WriteLine($"Error: Following exception occurred while getting relative path.\n{e}");
                        }
                        var destination = Path.Combine(packingDir, relativePath);
                        Directory.CreateDirectory(Directory.GetParent(destination).FullName);
                        File.Copy(fileItem.FileName, destination, true);
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

            string excludingParamString = "";
            if (excludedPaths.Count > 0)
            {
                var excludingParams = new List<string>(excludedPaths.Count);
                foreach (var item in excludedPaths)
                {
                    try
                    {
                        var relativePath = GetRelativePath(
                            new DirectoryInfo(WatchingPath).FullName,
                            new DirectoryInfo(item).FullName);
                        var relativeFilter = Path.Combine(relativePath, "*");
                        if (relativeFilter.StartsWith(@".\"))
                        {
                            // remove ".\" so 7z recognizes
                            relativeFilter = relativeFilter.Substring(2);
                        }
                        if (relativeFilter.StartsWith("@.."))
                        {
                            // ignore parent directory
                            continue;
                        }
                        excludingParams.Add("-xr0!" + relativeFilter);
                    }
                    catch (Exception e)
                    {
                        if (e is ArgumentException || e is FileNotFoundException)
                        {
                            Console.WriteLine($"Info: Relative path not found for {item}");
                            continue;
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
                excludingParamString = string.Join(" ", excludingParams.ToArray());
            }

            var arg = $"a {zipFileName} {filterListString} -r0 {excludingParamString}";
#if DEBUG
            Console.WriteLine($"Info: arg to 7z: {arg}");
#endif

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = zipExe,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = arg,
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
                Directory.Delete(packingDir, true);
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
