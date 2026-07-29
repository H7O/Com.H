using Com.H.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
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
        private static readonly JsonDocumentOptions _jsonOptions = new() { MaxDepth = 64 };

        /// <summary>
        /// Extracts parameters from a data model into a dictionary.
        /// </summary>
        /// <param name="dataModel">The data model to extract parameters from</param>
        /// <param name="descending">If true, later values overwrite earlier ones; if false, earlier values are preserved</param>
        /// <returns>Dictionary of parameter names to values</returns>
        /// <remarks>
        /// Retained as a distinct overload so that assemblies compiled against earlier versions of
        /// Com.H continue to bind. New code should call the three-parameter overload.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static IDictionary<string, object>? GetDataModelParameters(this object dataModel, bool descending)
            => GetDataModelParameters(dataModel, descending, false);

        /// <summary>
        /// Extracts parameters from various data model types into a dictionary.
        /// Supports anonymous objects, dictionaries (string-object or string-string), JsonElement, JSON strings, and regular objects.
        /// Nested JSON/XML structures are returned as raw JSON/XML text strings.
        /// </summary>
        /// <param name="dataModel">The data model to extract parameters from. Can be an anonymous object, Dictionary&lt;string,object&gt;,
        /// Dictionary&lt;string,string&gt;, JsonElement, JSON string, or any object with properties.</param>
        /// <param name="descending">If true, later values override earlier ones when duplicate keys are encountered. Default is false.</param>
        /// <param name="caseSensitive">If true, uses case-sensitive key comparison. If false (default), uses case-insensitive comparison.</param>
        /// <returns>A dictionary containing parameter names as keys and their values, or null if dataModel is null</returns>
        /// <example>
        /// <code>
        /// // From anonymous object (case-insensitive by default)
        /// var params1 = new { name = "John", age = 30 }.GetDataModelParameters();
        ///
        /// // From JsonElement
        /// var json = JsonDocument.Parse("{\"name\":\"Jane\",\"age\":25}").RootElement;
        /// var params2 = json.GetDataModelParameters();
        ///
        /// // Case-sensitive lookup
        /// var params3 = new { Name = "Bob" }.GetDataModelParameters(caseSensitive: true);
        /// </code>
        /// </example>
        public static IDictionary<string, object>? GetDataModelParameters(
            this object dataModel,
            bool descending = false,
            bool caseSensitive = false)
        {
            if (dataModel == null) return null;
            var comparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            Dictionary<string, object> result = new(comparer);
            foreach (var item in dataModel.EnsureEnumerable())
            {
                if (((object?)item) == null) continue;
                #region check for string object pair
                if (typeof(IDictionary<string, object>).IsAssignableFrom(item.GetType()))
                {
                    foreach (var x in ((IDictionary<string, object>)item))
                    {
                        if (result.ContainsKey(x.Key) && !descending) continue;
                        result[x.Key] = x.Value;
                    }
                    continue;
                }
                if (typeof(IEnumerable<KeyValuePair<string, object>>).IsAssignableFrom(item.GetType()))
                {
                    foreach (var x in ((IEnumerable<KeyValuePair<string, object>>)item))
                    {
                        if (result.ContainsKey(x.Key) && !descending) continue;
                        result[x.Key] = x.Value;
                    }
                    continue;
                }
                #endregion

                #region check for string string pair
                if (typeof(IDictionary<string, string>).IsAssignableFrom(item.GetType()))
                {
                    foreach (var x in ((IDictionary<string, string>)item))
                    {
                        if (result.ContainsKey(x.Key) && !descending) continue;
                        result[x.Key] = x.Value;
                    }
                    continue;
                }
                if (typeof(IEnumerable<KeyValuePair<string, string>>).IsAssignableFrom(item.GetType()))
                {
                    foreach (var x in ((IEnumerable<KeyValuePair<string, string>>)item))
                    {
                        if (result.ContainsKey(x.Key) && !descending) continue;
                        result[x.Key] = x.Value;
                    }
                    continue;
                }
                #endregion

                #region check for JsonElement
                if (typeof(JsonElement).IsAssignableFrom(item.GetType()))
                {
                    JsonElement json = (JsonElement)item;
                    if (json.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var x in json.EnumerateObject())
                        {
                            if (result.ContainsKey(x.Name) && !descending) continue;
                            switch (x.Value.ValueKind)
                            {
                                case JsonValueKind.False:
                                    result[x.Name] = false; break;
                                case JsonValueKind.True:
                                    result[x.Name] = true; break;
                                case JsonValueKind.Number:
                                    result[x.Name] = x.Value.GetDouble(); break;
                                case JsonValueKind.String:
                                    result[x.Name] = x.Value.GetString()!; break;
                                case JsonValueKind.Null:
                                    result[x.Name] = null!; break;
                                case JsonValueKind.Array:
                                case JsonValueKind.Object:
                                    // For nested structures, return the raw JSON text as a string
                                    result[x.Name] = x.Value.GetRawText();
                                    break;
                                default:
                                    result[x.Name] = x.Value.ToString();
                                    break;
                            }
                        }
                    }
                    continue;
                }
                #endregion

                #region check for string
                if (typeof(string) == item.GetType())
                {
                    try
                    {
                        var json = JsonDocument.Parse(item, _jsonOptions).RootElement;
                        if (json.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var x in json.EnumerateObject())
                            {
                                if (result.ContainsKey(x.Name) && !descending) continue;
                                switch (x.Value.ValueKind)
                                {
                                    case JsonValueKind.False:
                                        result[x.Name] = false; break;
                                    case JsonValueKind.True:
                                        result[x.Name] = true; break;
                                    case JsonValueKind.Number:
                                        result[x.Name] = x.Value.GetDouble(); break;
                                    case JsonValueKind.String:
                                        result[x.Name] = x.Value.GetString()!; break;
                                    case JsonValueKind.Null:
                                        result[x.Name] = null!; break;
                                    case JsonValueKind.Array:
                                    case JsonValueKind.Object:
                                        // For nested structures, return the raw JSON text as a string
                                        result[x.Name] = x.Value.GetRawText();
                                        break;
                                    default:
                                        result[x.Name] = x.Value.ToString();
                                        break;
                                }
                            }
                        }
                    }
                    catch { }
                    continue;
                }
                #endregion

                foreach (var x in ((object)item).GetType().GetProperties())
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
