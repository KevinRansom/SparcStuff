using System;
using Microsoft.DotNet.DependencyManager;

namespace AssemblyRunner
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.IO;
    using System.Timers;
    using System.IO.Compression;
    using System.Linq;

    static class Watcher
    {
        static DirectoryInfo currentDirectory;
        static DirectoryInfo filesDirectory;
        static DirectoryInfo packagesDirectory;

        static void ExecuteWorker(FileInfo worker)
        {
            Console.WriteLine($"Execute: {worker.FullName}");
        }

        static void UnpackPackages()
        {
            var packages = filesDirectory.GetFiles("*.nupkg");
            foreach(var package in packages)
            {
                var name = Path.GetFileNameWithoutExtension(package.Name);
                var build = name.LastIndexOf(".");
                var minor = build >=0 ? name.LastIndexOf(".", build - 1) : -1;
                var major = minor >= 0 ? name.LastIndexOf(".", minor - 1): -1;
                if (major >= 0)
                {
                    var packageName = name.Substring(0, major);
                    var packageDirectory = new DirectoryInfo(Path.Combine(packagesDirectory.FullName, packageName));
                    if (!packageDirectory.Exists)
                    {
                        ZipFile.ExtractToDirectory(package.FullName, packageDirectory.FullName);
                    }
                }
            }
        }

        static FileInfo FindHighestFile(string pattern)
        {
            var files = filesDirectory.GetFiles(pattern);
            if (files.Length >= 1)
            {
                Array.Sort(files);
                var file = files.Last();
                return new FileInfo(Path.Combine(currentDirectory.FullName, "files", file.Name));
            }

            return new FileInfo(Path.Combine(currentDirectory.FullName, "files", "FileNot.Found"));
        }

        static void DoWork(Object source, ElapsedEventArgs e)
        {
            timer.Enabled = false;

            // Find worker
            var file = FindHighestFile("interactive_worker_*.dll");
            if (file.Exists)
            {
                var assemblyPathsFile = FindHighestFile("assemblyPaths_*.probe");
                var assemblyDependencies = File.ReadAllLines(assemblyPathsFile.FullName);

                var nativeDepenciesFile = FindHighestFile("nativePaths_*.probe");
                var nativeDependencies = File.ReadAllLines(nativeDepenciesFile.FullName);

                IEnumerable<string> AssemblyProbingPaths() => assemblyDependencies;
                IEnumerable<string> NativeProbingRoots() => nativeDependencies;

                UnpackPackages();
                using (var dm = new DependencyProvider(AssemblyProbingPaths, NativeProbingRoots))
                {
                    var worker = new FileInfo(Path.Combine(currentDirectory.FullName, "files", file.Name));
                    ExecuteWorker(worker);
                }
            }
        }

        static Timer timer;

        static DirectoryInfo MakeDirectory(DirectoryInfo directory)
        {
            if (!directory.Exists)
            {
                directory.Create();
            }
            return directory;
        }

        private static void Main()
        {
            currentDirectory = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            filesDirectory = MakeDirectory(new DirectoryInfo(Path.Combine(currentDirectory.FullName, "files")));
            packagesDirectory = MakeDirectory(new DirectoryInfo(Path.Combine(currentDirectory.FullName, "packages")));

            // Watch the current directory a file named semaphore.flag
            // Wen the file is updated: Execute Dowork()

            // Create a timer with a two second interval.
            timer = new System.Timers.Timer(500);
            timer.Elapsed += DoWork;

            // Create a new FileSystemWatcher and set its properties.
            using (FileSystemWatcher watcher = new FileSystemWatcher())
            {
                watcher.Path = Path.Combine(currentDirectory.FullName);

                // Watch for changes in LastAccess and LastWrite times.
                // the renaming of files or directories.
                watcher.NotifyFilter = NotifyFilters.LastWrite;

                // Only watch text files.
                watcher.Filter = "semaphore.flag";

                // Add event handlers.
                watcher.Changed += OnChanged;

                // Begin watching.
                watcher.EnableRaisingEvents = true;

                // Wait for the user to quit the program.
                Console.WriteLine("Press 'q' to quit the sample.");
                while (Console.Read() != 'q') ;
            }
        }

        // Define the event handlers.
        private static void OnChanged(object source, FileSystemEventArgs e) => timer.Enabled = true;
    }
}
