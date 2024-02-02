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
            var parentFolder = (Directory.GetParent(path)?.FullName) ?? throw new ArgumentException($"Can't find parent folder of '{path}'");
            if (Directory.Exists(parentFolder))
                return path;
            Directory.CreateDirectory(parentFolder);
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
        /// Returns a temp file path without creating a zero byte file.
        /// </summary>
        /// <param name="basePath"></param>
        /// <returns></returns>
        public static string GetTempFilePath(string? basePath = null)
            => Path.Combine(basePath ?? Path.GetTempPath(), $"{Guid.NewGuid()}.tmp");

        /// <summary>
        /// Spins a task to attempt deleting an exclusively open file.
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
                (token == null ? Task.Delay(intervalBetweenAttempts) :
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

        /// <summary>
        /// Search files within a folder (and optionally its subfolders too) using a regex
        /// then returns an IEnumerable of FileInfo as the result
        /// </summary>
        /// <param name="basePath"></param>
        /// <param name="recursion"></param>
        /// <param name="regexFilter"></param>
        /// <returns></returns>
        public static IEnumerable<FileInfo> ListFiles(
            this string basePath,
            bool recursion = false,
            string? regexFilter = null)
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
                    .Where(x => recursion || !Directory.Exists(x))
                    .SelectMany(x => ListFiles(x, recursion, regexFilter)))
                    yield return fInfo;
            }
            yield break;
        }

        public static string UnifyPathSeperator(this string path)
        {
            if (string.IsNullOrWhiteSpace(path)
                ) return path;
            var oldPath = path;
            // string pathSeperator = Path.DirectorySeparatorChar + "";
            //string altSeperator = pathSeperator.Equals("/") ? "\\" : "/";
            path = path.Replace(Path.AltDirectorySeparatorChar,
                Path.DirectorySeparatorChar)
                .Replace("" + Path.DirectorySeparatorChar + Path.DirectorySeparatorChar,
                Path.DirectorySeparatorChar + "");
            if (!oldPath.Equals(path)) return UnifyPathSeperator(path);
            return path;
        }

        //public static string GetCurrentDirectory()
        //{
        //    #if ASPNETCORE50
        //    // return AppDomain.CurrentDomain.BaseDirectory
        //        return Directory.GetCurrentDirectory();
        //    #else
        //        return  Environment.CurrentDirectory;
        //    #endif
        //}

        public static MemoryStream ToMemoryStream(this string content, Encoding? encoding = null)
            => new((encoding ?? Encoding.UTF8).GetBytes(content));

        public static bool IsWritableFolder(this Uri? uri)
        {
            ArgumentNullException.ThrowIfNull(uri);
            if (!uri.IsFile) return false;
            return IsWritableFolder(uri.LocalPath);
        }
        public static bool IsWritableFolder(this string? folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) throw new ArgumentNullException(nameof(folderPath));
            try
            {
                using FileStream fs = File.Create(
                    Path.Combine(
                        folderPath,
                        Path.GetRandomFileName()
                    ),
                    1,
                    FileOptions.DeleteOnClose);
                return true;
            }
            catch { }
            return false;
        }
        /// <summary>
        /// Check if a file is in use by another process
        /// </summary>
        /// <param name="path">The path of the file to check</param>
        /// <returns>Returns true if the file is in use</returns>
        public static bool IsFileInUse(this string path)
        {
            try
            {
                // check if it's a folder
                if (Directory.Exists(path)) return false;
                using FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
                return false; // the file is not in use
            }
            catch (IOException)
            {
                return true; // the file is in use
            }
        }

        /// <summary>
        /// Check if a file is in use by another process
        /// </summary>
        /// <param name="fileInfo">The file to check</param>
        /// <returns>Returns true if the file is in use</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool IsFileInUse(this FileInfo fileInfo)
        {
            return fileInfo == null ? throw new ArgumentNullException(nameof(fileInfo)) 
                : IsFileInUse(fileInfo.FullName);
        }
    }
}
