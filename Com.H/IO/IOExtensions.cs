using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Com.H.IO
{
    public static class IOExtensions
    {
        /// <summary>
        /// Ensures creating the parent directories for "path" if they do not exist already. 
        /// </summary>
        /// <param name="path">file or folder path</param>
        public static void EnsureParentDirectory(this string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (path.IndexOfAny(Path.GetInvalidPathChars()) != -1)
                throw new ArgumentException($"{nameof(path)} contains invalid characters.");
            if (Directory.Exists(Directory.GetParent(path).FullName))
                return;
            Directory.CreateDirectory(Directory.GetParent(path).FullName);
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

    }
}
