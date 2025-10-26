using Com.H.Threading;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
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
            var parentFolder = Directory.GetParent(path)?.FullName;
            if (parentFolder == null) throw new ArgumentException($"Can't find parent folder of '{path}'");
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
        public static string GetTempFilePath(string basePath = null)
            => Path.Combine(basePath??Path.GetTempPath(), $"{Guid.NewGuid()}.tmp");

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
                    catch { }
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

        public static MemoryStream ToMemoryStream(this string content, Encoding encoding = null)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;
            return new MemoryStream((encoding??Encoding.UTF8).GetBytes(content));
        }

        public static bool IsWritableFolder(this Uri uri)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            if (!uri.IsFile) return false;
            return IsWritableFolder(uri.LocalPath);
        }
        public static bool IsWritableFolder(this string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) throw new ArgumentNullException(nameof(folderPath));
            try
            {
                using (FileStream fs = File.Create(
                    Path.Combine(
                        folderPath,
                        Path.GetRandomFileName()
                    ),
                    1,
                    FileOptions.DeleteOnClose)
                )
                {}
                return true;
            }
            catch { }
            return false;
        }


        #region file upload security checks
        private static readonly HashSet<string> WindowsReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        // Check for zero-width and other invisible Unicode characters
        // These can be used to bypass validation or hide malicious content
        private static readonly HashSet<char> InvisibleChars = new HashSet<char>() {
        '\u200B', // Zero-width space
        '\u200C', // Zero-width non-joiner
        '\u200D', // Zero-width joiner  
        '\u200E', // Left-to-right mark
        '\u200F', // Right-to-left mark
        '\uFEFF'  // Zero-width no-break space (BOM)
        };


        /// <summary>
        /// Validates a user-provided file name for security and compatibility issues.
        /// Prevents path traversal, invalid characters, reserved names, and ensures
        /// the file name is within safe length and character limits. Returns a
        /// normalized version of the file name.
        /// </summary>
        /// <param name="fileName">The user-provided file name to validate (not a path).</param>
        /// <param name="permittedFileExtensions">Extension whitelist (including the dot, e.g., ".txt"). If null or empty, all extensions are allowed.</param>
        /// <exception cref="ArgumentException">Thrown when the file name is invalid.</exception>
        /// <exception cref="SecurityException">Thrown when the file name could escape the base directory.</exception>
        /// <returns>Normalized file name</returns>
        /// <remarks>
        /// IMPORTANT: Extension whitelist configuration must include the dot prefix (e.g., ".txt", ".pdf")
        /// because Path.GetExtension() returns extensions in the format ".ext".
        /// </remarks>

        public static string ValidateAndGetNormalizeFileName(
            this string fileName,
            HashSet<string> permittedFileExtensions = null)
        {


            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be empty or whitespace.");
            }

            // Normalize to NFC form to prevent Unicode bypass attacks
            if (!fileName.IsNormalized(NormalizationForm.FormC))
            {
                fileName = fileName.Normalize(NormalizationForm.FormC);
            }


            // Check for zero - width and other invisible Unicode characters
            // These can be used to bypass validation or hide malicious content
            if (fileName.Any(c => InvisibleChars.Contains(c)))
            {
                throw new ArgumentException($"File name `{fileName}` contains invisible Unicode characters.");
            }

            // Check for control characters early (includes null bytes)
            // (0x00-0x1F)
            if (fileName.Any(c => char.IsControl(c)))
            {
                throw new ArgumentException($"File name `{fileName}` contains control characters.");
            }



            // Check for NTFS alternate data streams (Windows-specific attack,
            // but won't show up in Path.GetInvalidFileNameChars(), added for cross platform compatiblity
            // in case files were first uploaded to linux then accessed on Windows later, or copied to Windows)
            if (fileName.Contains(':'))
            {
                throw new ArgumentException($"File name `{fileName}` contains colon character (potential alternate data stream).");
            }

            // validate if file name has invalid characters
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                if (fileName.Contains(invalidChar))
                {
                    throw new ArgumentException($"File name `{fileName}` contains invalid character `{invalidChar}`");
                }
            }


            // validate if file name is too long
            if (fileName.Length > 150)
            {
                throw new ArgumentException($"File name `{fileName}` is too long. Maximum length is 150 characters.");
            }

            // validate if file name has path traversal characters
            if (fileName.Contains(".."))
            {
                throw new ArgumentException($"File name `{fileName}` contains invalid path traversal sequence `..`");
            }

            // validate if file name has directory separator characters
            if (fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains(Path.AltDirectorySeparatorChar))
            {
                throw new ArgumentException($"File name `{fileName}` contains invalid directory separator characters.");
            }

            // Check if the base filename (without extension) is reserved
            // although this is Windows specific, it's better to avoid using these names
            // in case the files are ever accessed on a Windows system (such as a Windows-based file share)
            // or copied to a Windows system
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            if (WindowsReservedNames.Contains(fileNameWithoutExtension))
            {
                throw new ArgumentException($"File name `{fileName}` uses a reserved Windows device name.");
            }

            // Trim and check for changes (also Windows specific, but good practice to have it on linux too for the same reasons as above)
            var trimmedFileName = fileName.Trim(' ', '.');
            if (trimmedFileName != fileName)
            {
                throw new ArgumentException($"File name cannot start or end with spaces or dots.");
            }

            // Check for files that are only dots (Windows restriction)
            if (fileName.All(c => c == '.'))
            {
                throw new ArgumentException($"File name cannot consist only of dots.");
            }


            // Check for leading hyphen (can cause issues with command-line tools)
            if (fileName.StartsWith("-"))
            {
                throw new ArgumentException($"File name cannot start with a hyphen.");
            }

            // Optional: Check for multiple extensions (uncomment if needed)
            // if (fileName.Count(c => c == '.') > 1)
            // {
            //     throw new ArgumentException($"File name `{fileName}` contains multiple extensions.");
            // }


            // base path hasn't yet been decided (to be decided in the next middleware), but for security validation purposes
            // we can assume a base path and check if the combined path escapes it
            var testBasePath = Path.GetTempPath();
            var testFullPath = Path.GetFullPath(Path.Combine(testBasePath, fileName));

            // Ensure the resolved path is within the base directory
            var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;


            if (!testFullPath.StartsWith(testBasePath, comparison))
            {
                throw new SecurityException($"File path escapes the base directory.");
            }


            if (permittedFileExtensions != null && permittedFileExtensions.Count > 0)
            {
                var fileExtension = Path.GetExtension(fileName);

                if (string.IsNullOrWhiteSpace(fileExtension))
                {
                    throw new ArgumentException("File must have an extension.");
                }

                if (!permittedFileExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"File extension `{fileExtension}` is not permitted.");
                }
            }
            return fileName;
        }

        #endregion

    }
}
