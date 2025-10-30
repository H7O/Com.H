using Com.H.Threading;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
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


        #region file name checks and validation
        public static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        // Check for zero-width and other invisible Unicode characters
        // These can be used to bypass validation or hide malicious content
        public static readonly HashSet<char> InvisibleChars = [
        '\u200B', // Zero-width space
        '\u200C', // Zero-width non-joiner
        '\u200D', // Zero-width joiner  
        '\u200E', // Left-to-right mark
        '\u200F', // Right-to-left mark
        '\uFEFF'  // Zero-width no-break space (BOM)
        ];


        // Precompute invalid file name characters for performance
        public static readonly SearchValues<char> InvalidFileNameChars =
        SearchValues.Create(Path.GetInvalidFileNameChars());



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
            string fileName,
            HashSet<string>? permittedFileExtensions = null)
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

            // this is a faster approach using SearchValues but won't give details about which character is invalid
            //if (fileName.AsSpan().IndexOfAny(InvalidFileNameChars)>-1)
            //{
            //    throw new ArgumentException($"File name `{fileName}` contains invalid characters.");
            //}



            // Find all invalid characters, this is a compromise between performance and detailed error reporting
            // it uses SearchValues for performance but collects all invalid characters found
            var span = fileName.AsSpan();
            var invalidChars = new HashSet<char>();
            int index = 0;

            while (index < span.Length)
            {
                int foundIndex = span[index..].IndexOfAny(InvalidFileNameChars);
                if (foundIndex == -1)
                    break;

                invalidChars.Add(span[index + foundIndex]);
                index += foundIndex + 1;
            }

            if (invalidChars.Count > 0)
            {
                throw new ArgumentException(
                    $"File name `{fileName}` contains invalid characters: {string.Join(", ", invalidChars.Select(c => $"`{c}`"))}");
            }



            // the below is the original way of checking invalid characters but it's slower
            //foreach (var invalidChar in Path.GetInvalidFileNameChars())
            //{
            //    if (fileName.Contains(invalidChar))
            //    {
            //        throw new ArgumentException($"File name `{fileName}` contains invalid character `{invalidChar}`");
            //    }
            //}


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

        #region base64 file stream conversions to temp



        /// <summary>
        /// Memory-efficient streaming base64 decode and write to a file and return it's size.
        /// Uses ArrayPool for buffer management and FromBase64Transform for chunked decoding
        /// </summary>
        public static async Task<long> WriteBase64ToFileAsync(
            this string base64Content,
            string filePath,
            long? maxFileSizeInBytes = null,
            CancellationToken? cancellationToken = null)
        {
            long totalBytesWritten = 0;

            CancellationToken cToken = cancellationToken ?? CancellationToken.None;

            try
            {
                await using var fileStream = new FileStream(
                    filePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true);

                using var transform = new FromBase64Transform();

                const int chunkSize = 4096; // Must be multiple of 4 for base64
                int offset = 0;

                while (offset < base64Content.Length)
                {
                    int length = Math.Min(chunkSize, base64Content.Length - offset);

                    // Ensure we're at a valid base64 boundary
                    if (offset + length < base64Content.Length && length % 4 != 0)
                    {
                        length = (length / 4) * 4;
                    }

                    if (length == 0)
                        break;

                    // Rent buffers from ArrayPool for zero-allocation processing
                    byte[] inputBuffer = ArrayPool<byte>.Shared.Rent(length);
                    byte[] outputBuffer = ArrayPool<byte>.Shared.Rent(length);

                    try
                    {
                        int bytesEncoded = Encoding.ASCII.GetBytes(
                            base64Content.AsSpan(offset, length),
                            inputBuffer);

                        bool isFinalBlock = (offset + length >= base64Content.Length);

                        if (isFinalBlock)
                        {
                            byte[] finalOutput = transform.TransformFinalBlock(inputBuffer, 0, bytesEncoded);
                            await fileStream.WriteAsync(finalOutput, cToken);
                            totalBytesWritten += finalOutput.Length;
                        }
                        else
                        {
                            int outputBytes = transform.TransformBlock(
                                inputBuffer, 0, bytesEncoded,
                                outputBuffer, 0);

                            await fileStream.WriteAsync(
                                outputBuffer.AsMemory(0, outputBytes),
                                cToken);

                            totalBytesWritten += outputBytes;
                        }

                        // Check size limit during processing
                        if (maxFileSizeInBytes.HasValue && totalBytesWritten > maxFileSizeInBytes.Value)
                        {
                            throw new ArgumentException($"File `{filePath}` exceeds the maximum allowed size of {maxFileSizeInBytes.Value} bytes");
                        }

                        offset += length;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(inputBuffer);
                        ArrayPool<byte>.Shared.Return(outputBuffer);
                    }
                }

                return totalBytesWritten;
            }
            catch
            {
                // Clean up temp file if something goes wrong
                try { File.Delete(filePath); } catch { }
                throw;
            }
        }

        /// <summary>
        /// Memory-efficient streaming base64 decode and write to temp file
        /// Uses ArrayPool for buffer management and FromBase64Transform for chunked decoding
        /// </summary>
        public static async Task<(string tempPath, long fileSize)> WriteBase64ToTempFileAsync(
            this string base64Content,
            long? maxFileSizeInBytes,
            string fileName,
            CancellationToken cancellationToken)
        {
            var tempPath = Path.GetTempFileName();
            long totalBytesWritten = 0;

            try
            {
                await using var fileStream = new FileStream(
                    tempPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true);

                using var transform = new FromBase64Transform();

                const int chunkSize = 4096; // Must be multiple of 4 for base64
                int offset = 0;

                while (offset < base64Content.Length)
                {
                    int length = Math.Min(chunkSize, base64Content.Length - offset);

                    // Ensure we're at a valid base64 boundary
                    if (offset + length < base64Content.Length && length % 4 != 0)
                    {
                        length = (length / 4) * 4;
                    }

                    if (length == 0)
                        break;

                    // Rent buffers from ArrayPool for zero-allocation processing
                    byte[] inputBuffer = ArrayPool<byte>.Shared.Rent(length);
                    byte[] outputBuffer = ArrayPool<byte>.Shared.Rent(length);

                    try
                    {
                        int bytesEncoded = Encoding.ASCII.GetBytes(
                            base64Content.AsSpan(offset, length),
                            inputBuffer);

                        bool isFinalBlock = (offset + length >= base64Content.Length);

                        if (isFinalBlock)
                        {
                            byte[] finalOutput = transform.TransformFinalBlock(inputBuffer, 0, bytesEncoded);
                            await fileStream.WriteAsync(finalOutput, cancellationToken);
                            totalBytesWritten += finalOutput.Length;
                        }
                        else
                        {
                            int outputBytes = transform.TransformBlock(
                                inputBuffer, 0, bytesEncoded,
                                outputBuffer, 0);

                            await fileStream.WriteAsync(
                                outputBuffer.AsMemory(0, outputBytes),
                                cancellationToken);

                            totalBytesWritten += outputBytes;
                        }

                        // Check size limit during processing
                        if (maxFileSizeInBytes.HasValue && totalBytesWritten > maxFileSizeInBytes.Value)
                        {
                            throw new ArgumentException($"File `{fileName}` exceeds the maximum allowed size of {maxFileSizeInBytes.Value} bytes");
                        }

                        offset += length;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(inputBuffer);
                        ArrayPool<byte>.Shared.Return(outputBuffer);
                    }
                }

                return (tempPath, totalBytesWritten);
            }
            catch
            {
                // Clean up temp file if something goes wrong
                try { File.Delete(tempPath); } catch { }
                throw;
            }
        }

        /// <summary>
        /// Calculate decoded size without fully decoding (for when content stays in JSON)
        /// </summary>
        public static long GetBase64DecodedSize(this string base64Content)
        {
            if (string.IsNullOrEmpty(base64Content))
                return 0;

            int padding = 0;
            if (base64Content.EndsWith("=="))
                padding = 2;
            else if (base64Content.EndsWith("="))
                padding = 1;

            return (base64Content.Length * 3L / 4L) - padding;
        }


        #endregion



    }
}
