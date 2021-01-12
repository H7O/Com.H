using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;

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

        //public static void WriteCsv(
        //    this IEnumerable<object> enumerables,
        //    System.IO.Stream outStream,
        //    bool excludeHeaders = false)
        //{
        //    foreach (var item in enumerables)
        //    {
        //        var properties = item?.GetType()?.GetCachedProperties()?.ToList();

        //        if (properties == null || properties.Count < 1) continue;

        //        #region headers
        //        if (!excludeHeaders && !headersSet)
        //        {
        //            sheetData.Append(
        //            new Row(
        //            properties.Select(pInfo => new Cell()
        //            {
        //                CellValue = new CellValue(pInfo.Name),
        //                DataType = new EnumValue<CellValues>(CellValues.String)
        //            })));
        //            headersSet = true;
        //        }
        //        #endregion
        //    }


        //}
    }

}