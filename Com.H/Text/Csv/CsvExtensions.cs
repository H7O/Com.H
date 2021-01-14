﻿using Com.H.Reflection;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Com.H.Text.Csv
{
    public static class CsvExtensions
    {
        /// <summary>
        /// Converts a list of string to comma seperated values string.
        /// </summary>
        /// <param name="enumerable">list of strings</param>
        /// <param name="delimiter">delimiter, default is comma ','</param>
        /// <returns></returns>
        public static string ToCsv(this IEnumerable<string> enumerable,
            string delimiter = ",")
            => enumerable == null ? "" :
            string.Join(delimiter, enumerable);


        public static IEnumerable<dynamic> ParseDelimited(this string text,
            string[] rowDelimieter, string[] colDelimieter)
        {
            if (rowDelimieter == null) throw new ArgumentNullException(nameof(rowDelimieter));
            if (colDelimieter == null) throw new ArgumentNullException(nameof(colDelimieter));
            var data = text.Split(rowDelimieter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var headers = data.First().Split(colDelimieter, StringSplitOptions.TrimEntries);
            var rows = data.Skip(1).Select(col =>
                col.Split(colDelimieter, StringSplitOptions.TrimEntries));

            return rows.Select(r =>
            {
                System.Dynamic.ExpandoObject exObj = new ExpandoObject();
                foreach (var item in r.Zip(headers, (c, h) => new { c, h }))
                    exObj.TryAdd(item.h, item.c);
                return (dynamic)exObj;
            });
        }

        public static IEnumerable<dynamic> ParseCsv(this string text)
            => text.ParseDelimited(new string[] { "\r", "\n" }, new string[] { "," });
        public static IEnumerable<dynamic> ParsePsv(this string text)
            => text.ParseDelimited(new string[] { "\r", "\n" }, new string[] { "|" });

        public static void WriteCsv(
            this IEnumerable<object> enumerables,
            System.IO.Stream outStream,
            CancellationToken? cancellationToken = null,
            string delimiter = ",",
            bool excludeHeaders = false,
            Encoding encoding = null
            )
        {
            enumerables.WriteCsvAsync(
                outStream,
                cancellationToken,
                delimiter,
                excludeHeaders,
                encoding
                ).GetAwaiter().GetResult();
        }
        public async static Task WriteCsvAsync(
            this IEnumerable<object> enumerables,
            System.IO.Stream outStream,
            CancellationToken? cancellationToken = null,
            string delimiter = ",",
            bool excludeHeaders = false,
            Encoding encoding = null
            )
        {
            
            bool headersSet = false;
            using var writer = new StreamWriter(outStream, encoding ??= Encoding.UTF8);
            foreach (var item in enumerables)
            {
                var properties = item?.GetCachedProperties()?.ToList();

                if (properties == null || properties.Count < 1) continue;

                #region headers
                if (!excludeHeaders && !headersSet)
                {
                    await writer.WriteAsync(
                        (properties.Select(x => x.Name)
                        .ToCsv(delimiter) + "\r\n").AsMemory(),
                        cancellationToken ?? default);
                    
                    await writer.FlushAsync();
                    headersSet = true;
                }
                #endregion

                #region data
                await writer.WriteAsync(
                    (properties
                    .Select(x => x.Info.GetValue(item)?.ToString())
                    .ToCsv(delimiter) + "\r\n").AsMemory(),
                    cancellationToken ?? default);
                await writer.FlushAsync();
                #endregion
            }
        }
        public static Stream ToCsvReader(
            this IEnumerable<object> enumerables,
            string delimiter = ",",
            bool excludeHeaders = false,
            Encoding encoding = null,
            string preferredTempFolderPath = null,
            string preferredTempFileName = null
            )
        {
            string excelOutputPath = enumerables.ToCsvTempFile(
                delimiter, 
                excludeHeaders, 
                encoding, 
                preferredTempFolderPath,
                preferredTempFileName);
            return new FileStream(excelOutputPath, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.DeleteOnClose);
        }

        public static string ToCsvTempFile(
            this IEnumerable<object> enumerables,
            string delimiter = ",",
            bool excludeHeaders = false,
            Encoding encoding = null,
            string preferredTempFolderPath = null,
            string preferredTempFileName = null
            )
        {
            string tempBasePath =
                Path.Combine(
                (string.IsNullOrEmpty(preferredTempFolderPath) ?
                Path.GetTempPath() : preferredTempFolderPath));

            var path =
                Path.Combine(tempBasePath,
                (string.IsNullOrEmpty(preferredTempFileName) ? Guid.NewGuid().ToString()
                : preferredTempFileName) + ".csv");

            Directory.CreateDirectory(tempBasePath);

            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch { }
            }
            using (StreamWriter f = File.CreateText(path))
            {
                enumerables.WriteCsvAsync(f.BaseStream, null, 
                    delimiter,
                    excludeHeaders,
                    encoding).GetAwaiter().GetResult();
                f.Close();
            }
            return path;
        }


    }

}