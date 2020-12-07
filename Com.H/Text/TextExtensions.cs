using Com.H.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Com.H.Text
{
    public static class TextExtensions
    {
        /// <summary>
        /// Performs case insensitive comparision between two strings.
        /// Also, this extension method returns true if both strings are null
        /// </summary>
        /// <param name="originalString"></param>
        /// <param name="stringToCompare"></param>
        /// <returns></returns>
        public static bool EqualsIgnoreCase(
            this string originalString,
            string stringToCompare)
            => originalString.IsNullEqual(stringToCompare) ??
                originalString
                .ToUpper(CultureInfo.InvariantCulture)
                .Equals(stringToCompare.ToUpper(CultureInfo.InvariantCulture));

        /// <summary>
        /// Performs case insensitive StartsWith string comparison.
        /// Also, this extension method returns true if both strings are null
        /// </summary>
        /// <param name="originalString"></param>
        /// <param name="stringToCompare"></param>
        /// <returns></returns>

        public static bool StartsWithIgnoreCase(
            this string originalString,
            string stringToCompare
            )
            => originalString.IsNullEqual(stringToCompare) ??
                originalString
                .ToUpper(CultureInfo.InvariantCulture)
                .StartsWith(stringToCompare.ToUpper(CultureInfo.InvariantCulture),
                    StringComparison.InvariantCultureIgnoreCase);

        /// <summary>
        /// Performs case insensitive EndsWith string comparison.
        /// Also, this extension method returns true if both strings are null
        /// </summary>
        /// <param name="originalString"></param>
        /// <param name="stringToCompare"></param>
        /// <returns></returns>

        public static bool EndsWithIgnoreCase(
            this string originalString,
            string stringToCompare
            )
            => originalString.IsNullEqual(stringToCompare) ??
                originalString
                .ToUpper(CultureInfo.InvariantCulture)
                .EndsWith(stringToCompare.ToUpper(CultureInfo.InvariantCulture),
                    StringComparison.InvariantCultureIgnoreCase);

        /// <summary>
        /// Returns true if subString is found within originalString.
        /// Also, this extension method returns true if both strings are null
        /// </summary>
        /// <param name="originalString"></param>
        /// <param name="stringToCompare"></param>
        /// <returns></returns>

        public static bool ContainsIgnoreCase(
            this string oringalString,
            string subString)
            => oringalString.IsNullEqual(subString) ??
                oringalString
                .ToUpper(CultureInfo.InvariantCulture)
                .Contains(subString.ToUpper(CultureInfo.InvariantCulture));

        /// <summary>
        /// Extract all occurances of integers from a string and return them in an IEnumerable of int.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static IEnumerable<int> ExtractInts(this string text)
        {
            foreach (var match in Regex.Matches(text, @"-?\d+", RegexOptions.Singleline))
                yield return int.Parse(match.ToString(), CultureInfo.InvariantCulture);
        }


        public static IEnumerable<int> ExtractRangeInts(this string text)
        =>
            Regex.Replace(text, @"\s+", "")
                .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(group => Regex.Matches(group, @"\d+").Cast<Match>())
                .Select(match => new
                {
                    Max = match.Max(v => int.Parse(v.Value, CultureInfo.InvariantCulture)),
                    Min = match.Min(v => int.Parse(v.Value, CultureInfo.InvariantCulture))
                }).SelectMany(range => Enumerable.Range(range.Min, range.Max - range.Min + 1))
                .Distinct().OrderBy(x => x);


        private static bool? IsNullEqual(
            this string originalString,
            string stringToCompare)
        {
            if (originalString == null && stringToCompare == null) return true;
            if ((originalString != null && stringToCompare == null)
                ||
                (originalString == null && stringToCompare != null)
                ) return false;
            return null;
        }

        private static string DictionaryParameterizedReplace(
            this string text, 
            IDictionary<string, object> parameters,
            string openingMarker = null,
            string closingMarker = null,
            string nullValueReplacement = null
            )
        {
            if (string.IsNullOrEmpty(text)
                ||
                (
                    parameters == null
                    ||
                    parameters.Count<1
                    && 
                    (openingMarker == null || closingMarker == null)
                )
                ) return text;

            
            var paramList = (openingMarker==null || closingMarker == null)?
                parameters.Keys.ToList()
                :Regex.Matches(text, openingMarker + @"(?<param>.*?)?" + closingMarker)
                .Cast<Match>()
                .Select(x => x.Groups["param"].Value)
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x).Distinct().ToList();

            if (paramList.Count > 0)
            {
                var joined = paramList
                    .LeftJoin(parameters,
                    pl => pl.ToUpper(CultureInfo.InvariantCulture),
                    p => p.Key.ToUpper(CultureInfo.InvariantCulture),
                    (pl, p) => new { k = pl, v = p.Value }).ToList();

                foreach (var item in joined)
                {
                    text = item.v == null?
                            text.Replace(openingMarker + item.k + closingMarker,
                            nullValueReplacement ?? "")
                    :
                    text.Replace(openingMarker + item.k + closingMarker,
                            Convert.ChangeType(item.v, TypeCode.String, CultureInfo.InvariantCulture) as string
                            );

                }

            }
            return text;
        }

        /// <summary>
        /// Fills a string having placeholders with information from a data model that has property names matching the string placeholders
        /// 
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dataModel">Could be any class with properties matching the src string placeholders, 
        /// or an IDictionary<string, object> where the IDictionary key is the property name, and the value is the placeholder replacement value</param>
        /// <param name="openingMarker">placeholder opening marker, e.g. {{</param>
        /// <param name="closingMarker">placeholder closing marker, e.g. }}</param>
        /// <param name="nullValueReplacement">a default value for placeholders that don't have a matching property name in the data model</param>
        /// <returns></returns>
        public static string Fill(
            this string src,
            object dataModel,
            string openingMarker = null,
            string closingMarker = null,
            string nullValueReplacement = null
            )=>
            DictionaryParameterizedReplace(
                src, 
                dataModel == null ? null
                :
                typeof(IDictionary<string, object>).IsAssignableFrom(dataModel.GetType())
                ?
                ((IDictionary<string, object>)dataModel)
                :
                dataModel.GetType().GetProperties()
                                .ToDictionary(k => k.Name, v => v.GetValue(dataModel, null)),
                openingMarker, 
                closingMarker, 
                nullValueReplacement
                );
            
    }
}
