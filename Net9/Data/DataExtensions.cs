using Com.H.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Com.H.Data
{

    /// <summary>
    /// Represents parameters for query placeholder replacement operations.
    /// </summary>
    public class QueryParams
    {
        /// <summary>
        /// The data model containing values to replace placeholders with.
        /// </summary>
        public object? DataModel { get; set; }
        /// <summary>
        /// The opening marker for placeholders (default: "{{").
        /// </summary>
        public string? OpenMarker { get; set; } = "{{";
        /// <summary>
        /// The closing marker for placeholders (default: "}}").
        /// </summary>
        public string? CloseMarker { get; set; } = "}}";
        /// <summary>
        /// The replacement value for null values (default: "null").
        /// </summary>
        public string? NullReplacement { get; set; } = "null";
        /// <summary>
        /// The regex pattern used to match parameter names within markers.
        /// </summary>
        public static string RegexPattern { get; set; } = @"(?<param>.*?)?";
        
    }
    /// <summary>
    /// Provides extension methods for data manipulation and query parameter operations.
    /// </summary>
    public static class DataExtensions
    {
        /// <summary>
        /// Extracts parameters from a data model into a dictionary.
        /// Supports objects, dictionaries, and enumerables of objects.
        /// </summary>
        /// <param name="dataModel">The data model to extract parameters from</param>
        /// <param name="descending">If true, later values overwrite earlier ones; if false, earlier values are preserved</param>
        /// <returns>Dictionary of parameter names to values</returns>
        public static IDictionary<string, object>? GetDataModelParameters(this object dataModel, bool descending = false)
        {
            if (dataModel == null) return null;
            Dictionary<string, object> result = new();
            foreach (var item in dataModel.EnsureEnumerable())
            {
                if (item == null) continue;
                if (typeof(IDictionary<string, object>).IsAssignableFrom(item.GetType()))
                {
                    foreach (var x in ((IDictionary<string, object>)item))
                    {
                        if (result.ContainsKey(x.Key) && !descending) continue;
                        result[x.Key] = x.Value;
                    }
                    continue;
                }
                foreach(var x in ((object)item).GetType().GetProperties())
                {
                    if (result.ContainsKey(x.Name) && !descending) continue;
                    result[x.Name] = x.GetValue(item, null);
                }
            }
            return result;
        }
        /// <summary>
        /// Replaces query parameter markers in a string with different markers.
        /// Useful for converting between different placeholder formats.
        /// </summary>
        /// <param name="query">The query string containing placeholders</param>
        /// <param name="srcOpenMarker">The source opening marker</param>
        /// <param name="srcCloseMarker">The source closing marker</param>
        /// <param name="dstOpenMarker">The destination opening marker</param>
        /// <param name="dstCloseMarker">The destination closing marker</param>
        /// <returns>The query string with replaced markers</returns>
        public static string ReplaceQueryParameterMarkers(
            this string query,
            string srcOpenMarker,
            string srcCloseMarker,
            string dstOpenMarker,
            string dstCloseMarker)
        {
            if (string.IsNullOrEmpty(query)) return query;
            var regexPattern = srcOpenMarker + QueryParams.RegexPattern + srcCloseMarker;
            var paramList = Regex.Matches(query, regexPattern)
                .Cast<Match>()
                .Select(x => x.Groups["param"].Value)
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x).Distinct().ToList();

            foreach (var item in paramList)
            {
                query = query.Replace(srcOpenMarker + item + srcCloseMarker,
                    dstOpenMarker + item + dstCloseMarker);
            }

            return query;
        }
    }
}
