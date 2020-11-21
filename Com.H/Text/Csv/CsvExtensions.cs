using System;
using System.Collections.Generic;
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
    }
}
