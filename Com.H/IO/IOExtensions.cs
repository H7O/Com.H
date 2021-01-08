using Com.H.Threading;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Com.H.IO
{
    public static class IOExtensions
    {
        /// <summary>
        /// Ensures creating the parent directories for "path" if they do not exist already. 
        /// </summary>
        /// <param name="path">file or folder path</param>
        public static string EnsureParentDirectory(this string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (path.IndexOfAny(Path.GetInvalidPathChars()) != -1)
                throw new ArgumentException($"{nameof(path)} contains invalid characters.");
            if (Directory.Exists(Directory.GetParent(path).FullName))
                return path;
            Directory.CreateDirectory(Directory.GetParent(path).FullName);
            return path;
        }

        /// <summary>
        /// Formats DateTime to directory path string yyyy/MMM/dd (or yyyy\MMM\dd depending on host OS) 
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static string ToDirectoryPath(this DateTime dateTime)
            =>
                Path.Combine(dateTime.ToString("yyyy"),
                                dateTime.ToString("MMM"),
                                dateTime.ToString("dd"));
        
        /// <summary>
        /// Formats DateTime to directory path string yyyy/MMM/dd (or yyyy\MMM\dd depending on host OS) 
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static string ToDirectoryPath(this DateTime? dateTime)
        {
            if (dateTime == null) throw new ArgumentNullException(nameof(dateTime));
            return ((DateTime)dateTime).ToDirectoryPath();
        }

        /// <summary>
        /// String representation of Path.DirectorySeparatorChar to eliminate the need to 
        /// escape backslash '\' character during string concatination on Windows.
        /// </summary>
        public static string DirectorySeperatorString { get; }
            = (Path.DirectorySeparatorChar == '\\' ? "\\" :
                Path.DirectorySeparatorChar + "");

        /// <summary>
        /// Returns a temp file path.
        /// </summary>
        /// <param name="basePath"></param>
        /// <returns></returns>
        public static string GetTempFilePath(string basePath = null)
            => Path.Combine(basePath??Path.GetTempPath(), $"{Guid.NewGuid()}.tmp");

        /// <summary>
        /// Spin a task to attempt deleting an exclusively open file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="miliseconds"></param>
        /// <param name="persistCount"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static Task PersistantDelete(
            this string path,
            int deleteAttempts = 5,
            int intervalBetweenAttempts = 1000, 
            CancellationToken? token = null)
        {
            if (string.IsNullOrEmpty(path)) return Task.CompletedTask;
            if (deleteAttempts < 1) deleteAttempts = 1;
            void Delay()
                =>
                (token == null?Task.Delay(intervalBetweenAttempts) :
                    Task.Delay(intervalBetweenAttempts, (CancellationToken)token))
                        .GetAwaiter().GetResult();

            bool IsCancelled() => token != null
                            &&
                            ((CancellationToken)token).IsCancellationRequested;
            
            void Delete()
            {
                int persist = 1;
                while (Directory.Exists(path))
                {
                    try
                    {
                        Directory.Delete(path, true);
                    }
                    catch { Console.WriteLine("nope"); }
                    if ((persist++) >= deleteAttempts) return;
                    Delay();
                    if (IsCancelled()) return;
                }
                while (File.Exists(path))
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch { }
                    if ((persist++) >= deleteAttempts) return;
                    Delay();
                    if (IsCancelled()) return;

                }
            }

            var task = (token == null ?
                Task.Run(Delete)
                : Task.Run(Delete, (CancellationToken)token));
            task.ConfigureAwait(true);
            return task;
        }

        public static IEnumerable<FileInfo> ListFiles(
            this string basePath, 
            bool recursion = false, 
            string regexFilter = null)
        {
            if (string.IsNullOrWhiteSpace(basePath)) yield break;
            if (File.Exists(basePath))
            {
                if (regexFilter == null || Regex.IsMatch(basePath, regexFilter))
                    yield return new FileInfo(basePath);
                yield break;
            }
            
            if (Directory.Exists(basePath))
            {
                foreach (var fInfo in Directory.GetFiles(basePath)
                    .Union(Directory.GetDirectories(basePath))
                    .Where(x=>recursion || !Directory.Exists(x))
                    .SelectMany(x => ListFiles(x, recursion, regexFilter)))
                    yield return fInfo;
            }
            yield break;
        }

    }
}
