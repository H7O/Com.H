using Com.H.Data;
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
        {
            var isNullEqual = originalString.IsNullEqual(stringToCompare);
            if (isNullEqual!=null) return isNullEqual.Value;
            return  
                originalString
                .ToUpper(CultureInfo.InvariantCulture)
                .Equals(stringToCompare.ToUpper(CultureInfo.InvariantCulture));

        }

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
        {
            var isNullEqual = originalString.IsNullEqual(stringToCompare);
            if (isNullEqual != null) return isNullEqual.Value;
            return originalString
                .ToUpper(CultureInfo.InvariantCulture)
                .StartsWith(stringToCompare.ToUpper(CultureInfo.InvariantCulture),
                    StringComparison.InvariantCultureIgnoreCase);
        }


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
        {
            var isNullEqual = originalString.IsNullEqual(stringToCompare);
            if (isNullEqual != null) return isNullEqual.Value;
            return originalString
                .ToUpper(CultureInfo.InvariantCulture)
                .EndsWith(stringToCompare.ToUpper(CultureInfo.InvariantCulture),
                    StringComparison.InvariantCultureIgnoreCase);
        }

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
        {
            var isNullEqual = oringalString.IsNullEqual(subString);
            if (isNullEqual != null) return isNullEqual.Value;
            return oringalString
                .ToUpper(CultureInfo.InvariantCulture)
                .Contains(subString.ToUpper(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Extract all occurances of integers from a string and return them in an IEnumerable of int.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static IEnumerable<int> ExtractInts(this string text)
        {
            foreach (var match in Regex.Matches(text, @"-?\d+", RegexOptions.Singleline)
                .Cast<Match>().Where(m=>m is not null))
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


        public static string ExtractAlphabet(this string text)
            => Regex.Matches(text, "[a-z]+", RegexOptions.IgnoreCase)
            .Cast<Match>()
                .Aggregate("", (i, n) => i + n.Value);
        


        private static bool? IsNullEqual(
            this string? originalString,
            string? stringToCompare)
        {
            if (originalString == null && stringToCompare == null) return true;
            if ((originalString != null && stringToCompare == null)
                ||
                (originalString == null && stringToCompare != null)
                ) return false;
            return null;
        }




        /// <summary>
        /// Fills a string having placeholders with information from a data model that has property names matching the string placeholders
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dataModel">Could be any class with properties matching the src string placeholders, 
        /// or an IDictionary<string, object> where the IDictionary key is the property name, and the value is the placeholder replacement value</param>
        /// <param name="openingMarker">placeholder opening marker, e.g. {{</param>
        /// <param name="closingMarker">placeholder closing marker, e.g. }}</param>
        /// <param name="nullValueReplacement">a default value for placeholders that don't have a matching property name in the data model</param>
        /// <returns></returns>
        /// 
        // deprected, the newer Fill supports IEnumerable of data models
        //public static string Fill(
        //    this string src,
        //    object dataModel,
        //    string openingMarker = null,
        //    string closingMarker = null,
        //    string nullValueReplacement = null
        //    ) =>
        //    DictionaryParameterizedReplace(
        //        src,
        //        dataModel == null ? null
        //        :
        //        typeof(IDictionary<string, object>).IsAssignableFrom(dataModel.GetType())
        //        ?
        //        ((IDictionary<string, object>)dataModel)
        //        :
        //        dataModel.GetType().GetProperties()
        //                        .ToDictionary(k => k.Name, v => v.GetValue(dataModel, null)),
        //        openingMarker,
        //        closingMarker,
        //        nullValueReplacement
        //        );





        /// <summary>
        /// Fills a string having placeholders with date formatted in accordance to format string enclosed inside placeholder markers
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dataModel">Could be any class with properties matching the src string placeholders, 
        /// or an IDictionary<string, object> where the IDictionary key is the property name, and the value is the placeholder replacement value</param>
        /// <param name="openingMarker">placeholder opening marker, e.g. {{</param>
        /// <param name="closingMarker">placeholder closing marker, e.g. }}</param>
        /// <param name="nullValueReplacement">a default value for placeholders that don't have a matching property name in the data model</param>
        /// <returns></returns>
        public static string FillDate(
            this string src,
            DateTime? date = null,
            string? openingMarker = "{date{",
            string? closingMarker = "}}",
            string? nullValueReplacement = null
            )
        {
            if (string.IsNullOrEmpty(openingMarker)) throw new ArgumentNullException(nameof(openingMarker));
            if (string.IsNullOrEmpty(closingMarker)) throw new ArgumentNullException(nameof(closingMarker));
            // Each format as written gets its own value: MM (month) and mm (minutes) differ only in case.
            return src.ReplaceInTextAsWritten(
                Regex.Matches(src,
                    Regex.Escape(openingMarker!) + @"(?<param>.*?)?" + Regex.Escape(closingMarker!))
                .Cast<Match>()
                .Select(x => x.Groups["param"].Value)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct()
                .Select(format => (openingMarker + format + closingMarker,
                    date == null ? DateTime.Now.ToString(format) : ((DateTime)date).ToString(format)))
                .ToList(),
                StringComparison.Ordinal);
        }

        /// <summary>
        /// Performs a case-insensitive search for a string within an IEnumerable and returns true if found.
        /// </summary>
        /// <param name="list"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        public static bool ContainsIgnoreCase(
            this IEnumerable<string> list,
            string item)
            => list?.Select(x => x.ToUpperInvariant())?
            .Contains(item?.ToUpperInvariant()) ?? false;

        /// <summary>
        /// Extracts dates from a string, if the dates are partial dates without a year,
        /// then this method extract them and returns them as dates by adding the current year.
        /// E.g. for partial date: 'Nov 5' or '11-05'
        /// </summary>
        /// <param name="text"></param>
        /// <param name="seperators"></param>
        /// <returns></returns>
        public static IEnumerable<DateTime> ExtractDates(
            this string text, 
            string[]? seperators = null)
        {

            var dates_string = text.Split(seperators?? new string[] { "|" },
                  StringSplitOptions.RemoveEmptyEntries
#if NET5_0_OR_GREATER
                  | StringSplitOptions.TrimEntries
#endif
                  )
                .AsEnumerable();
#if !NET5_0_OR_GREATER
            dates_string = dates_string.Select(x => x.Trim());
#endif

            // normal
            var normal = dates_string
                  .Where(x => DateTime.TryParse(x, out _));

            dates_string = dates_string.Except(normal);

            // without year (MM-dd)
            var MM_dd = dates_string.Where(x => Regex.IsMatch(x, @"d{1,2}\s*-\s*d{1,2}"));
            dates_string = dates_string.Except(MM_dd);

            // without year (MMM dd)
            var MMM_dd = dates_string.Where(x => Regex.IsMatch(x, @"[a-zA-Z]+\sd{1,2}"));
            //dates_string = dates_string.Except(MMM_dd);
            MMM_dd = MMM_dd.Select(x => Regex.Replace(x, @"[ ]{2,}", " "));

            return normal.Select(x => DateTime.Parse(x))
                .Union(MM_dd.Select(x => DateTime.Parse($"{DateTime.Now.Year}-{x}")))
                .Union(MMM_dd.Select(x => DateTime.Parse($"{x}, {DateTime.Now.Year}")));
        }

        /// <summary>
        /// Fills a string having placeholders with information from a data model that has property names matching the string placeholders.
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dataModel">Could be any class with properties matching the src string placeholders, 
        /// or an IDictionary&lt;string, object&gt; where the IDictionary key is the property name, and the value is the placeholder replacement value
        /// or it could be an IEnumerable&lt;dynamic&gt; where each element of the enumerable could have different properties.
        /// </param>
        /// <param name="openingMarker">placeholder opening marker, e.g. {{</param>
        /// <param name="closingMarker">placeholder closing marker, e.g. }}</param>
        /// <param name="nullValueReplacement">a default value for placeholders that don't have a matching property name in the data model</param>
        /// <returns></returns>
        /// <remarks>
        /// With both markers, each placeholder (openingMarker, a name, closingMarker, the markers matched as
        /// written) takes the value of the first data model that has a non-null value under its name, the
        /// name spelled the same way first, then one that differs only in case. A placeholder that no data
        /// model has a value for, or an empty one, becomes nullValueReplacement where the pattern found it,
        /// while a value is written wherever its placeholder's text appears. Values are written in the invariant culture
        /// and never read again: text inside a value that looks like a placeholder stays as it is.
        /// Without both markers, each property name's own text (with the one marker given, if any) is
        /// replaced by the first non-null value under that exact name, and nullValueReplacement is not used.
        /// An IEnumerable&lt;QueryParams&gt; is filled by <see cref="Fill(string, IEnumerable{QueryParams})"/>,
        /// with the markers and null replacement given here, when given, in place of each set's own.
        /// </remarks>
        public static string Fill(
            this string src,
            object? dataModel,
            string? openingMarker = null,
            string? closingMarker = null,
            string? nullValueReplacement = null
            )
        {
            if (dataModel == null) return src;
            
            if (typeof(IEnumerable<QueryParams>).IsAssignableFrom(dataModel.GetType()))
            {
                IEnumerable<QueryParams> queryParams = (IEnumerable<QueryParams>)dataModel;
                if (openingMarker is null
                    && closingMarker is null
                    && nullValueReplacement is null)
                    return Fill(src, queryParams);
                // The markers and null replacement given here take the place of each set's own.
                return Fill(src, queryParams.Where(x => x != null).Select(x =>
                    new QueryParams()
                    {
                        DataModel = x.DataModel,
                        OpenMarker = openingMarker ?? x.OpenMarker,
                        CloseMarker = closingMarker ?? x.CloseMarker,
                        NullReplacement = nullValueReplacement ?? x.NullReplacement
                    }
                ));
            }

            List<IDictionary<string, object?>> parameters = new();

            foreach (var item in dataModel.EnsureEnumerable().Where(x=>x is not null))
            {
                parameters.Add(
                          typeof(IDictionary<string, object?>).IsAssignableFrom(item.GetType())
                          ?
                          ((IDictionary<string, object?>)item)
                          :
                          ((object) item).GetType().GetProperties()
                                          .ToDictionary(k => k.Name, v => v?.GetValue(item, null))
                    );
            }

            return DictionaryParameterizedReplace(
              src,
              parameters,
              openingMarker,
              closingMarker,
              nullValueReplacement
                );
        }

        /// <summary>
        /// Fills a string with placeholders using multiple QueryParams configurations.
        /// Each QueryParams can have different opening/closing markers and data models.
        /// </summary>
        /// <remarks>
        /// Each set finds its placeholders (its OpenMarker, a name matching
        /// <see cref="QueryParams.RegexPattern"/>, its CloseMarker, the markers matched as written) in the
        /// text as written. Of the sets that found a placeholder, the latest one in the list that has its
        /// name fills it: with its value, or with its NullReplacement when that value is null. A set never
        /// fills a placeholder written with another set's markers. A placeholder whose name none of the
        /// sets that found it has becomes the NullReplacement of the latest of them. Names are looked up
        /// ignoring case, and each spelling of a placeholder is replaced wherever it appears exactly as
        /// written, character by character, whatever the culture. The values written in are never read
        /// again: text inside a value that looks like a placeholder stays as it is.
        /// </remarks>
        /// <param name="src">The source string containing placeholders</param>
        /// <param name="queryParams">Collection of QueryParams configurations to apply</param>
        /// <returns>String with all placeholders filled</returns>
        public static string Fill(
            this string src,
            IEnumerable<QueryParams>? queryParams
            )
        {
            if (queryParams == null || string.IsNullOrEmpty(src)) return src;

            // The latest set comes first, so it takes a placeholder that several sets find.
            var placeholders = queryParams.Where(x => x != null).Reverse()
                .SelectMany(x =>
                {
                    var values = x.DataModel?.GetDataModelParameters();
                    return Regex.Matches(src,
                            Regex.Escape(x.OpenMarker ?? "") + QueryParams.RegexPattern
                            + Regex.Escape(x.CloseMarker ?? ""))
                        .Cast<Match>()
                        .Select(m => m.Groups["param"].Value)
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                        .Distinct()
                        .Select(name =>
                        {
                            object? value = null;
                            var found = values?.TryGetValue(name, out value) == true;
                            return (Text: x.OpenMarker + name + x.CloseMarker, Found: found,
                                Value: value?.ToString() ?? x.NullReplacement ?? "");
                        });
                })
                .ToList();

            // Every set writes the names it has before any set writes a NullReplacement for a name it
            // doesn't have, so a name the latest set lacks is still filled from an earlier set. The
            // search is exact: each spelling ({{Name}}, {{NAME}}) is a match of its own, and the values
            // ignore case already.
            return src.ReplaceInTextAsWritten(
                placeholders.Where(x => x.Found).Concat(placeholders.Where(x => !x.Found))
                    .Select(x => (x.Text, x.Value)),
                StringComparison.Ordinal);
        }


        private static string DictionaryParameterizedReplace(
            this string text,
            IEnumerable<IDictionary<string, object?>>? parametersEnumerable,
            string? openingMarker = null,
            string? closingMarker = null,
            string? nullValueReplacement = null
            )
        {
            if (string.IsNullOrEmpty(text)
                ||
                (
                    parametersEnumerable == null
                    ||
                    !parametersEnumerable.Any()
                    &&
                    (openingMarker == null || closingMarker == null)
                )
                ) return text;

            var models = parametersEnumerable.Where(x => x != null).ToList();

            // Without both markers, a key's own text (with the one marker given, if any) is its
            // placeholder, and the first model with a value for it fills it.
            if (openingMarker == null || closingMarker == null)
                return text.ReplaceInTextAsWritten(
                    models.SelectMany(model => model
                        .Where(x => x.Value != null)
                        .Select(x => (openingMarker + x.Key + closingMarker, ValueText(x.Value)))),
                    StringComparison.Ordinal);

            // Each placeholder takes the value of the first model that has one for its name; an empty
            // one ({{}}) has none.
            var matches = Regex.Matches(text,
                    Regex.Escape(openingMarker) + @"(?<param>.*?)?" + Regex.Escape(closingMarker))
                .Cast<Match>()
                .ToList();
            var ignoringCase = new Dictionary<string, object>?[models.Count];
            var valueOfName = new Dictionary<string, object?>(StringComparer.Ordinal);
            var names = new List<string>();
            foreach (var match in matches)
            {
                var name = match.Groups["param"].Value;
                if (valueOfName.ContainsKey(name)) continue;
                object? value = null;
                for (var m = 0; m < models.Count && value == null && name.Length > 0; m++)
                    value = ValueOf(models[m], ref ignoringCase[m], name);
                valueOfName.Add(name, value);
                names.Add(name);
            }
            // A value fills its placeholder wherever it appears. The ones without a value are written
            // last, where the pattern found them, so they never take text a value would.
            return text.ReplaceInTextAsWritten(
                names.Where(name => valueOfName[name] != null)
                    .Select(name => (openingMarker + name + closingMarker, ValueText(valueOfName[name]))),
                StringComparison.Ordinal,
                matches.Where(x => valueOfName[x.Groups["param"].Value] == null)
                    .Select(x => (x.Index, x.Length, nullValueReplacement ?? "")));
        }

        /// <summary>
        /// The value under <paramref name="name"/>: the key spelled the same way first, then a key
        /// that differs only in case, looked up in <paramref name="ignoringCase"/>, which is built from
        /// the model on the first miss. Null when neither has a non-null value.
        /// </summary>
        private static object? ValueOf(
            IDictionary<string, object?> model,
            ref Dictionary<string, object>? ignoringCase,
            string name)
        {
            if (model.TryGetValue(name, out var value) && value != null) return value;
            if (ignoringCase == null)
            {
                // The first key in the model's order wins among keys that differ only in case.
                ignoringCase = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in model)
                    if (pair.Value != null && !ignoringCase.ContainsKey(pair.Key))
                        ignoringCase.Add(pair.Key, pair.Value);
            }
            return ignoringCase.TryGetValue(name, out var other) ? other : null;
        }

        /// <summary>
        /// A value as text, in the invariant culture. A value that is not IConvertible uses its
        /// IFormattable.ToString, or else its ToString.
        /// </summary>
        private static string ValueText(object? value)
            => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

        /// <summary>
        /// Replaces each pair's text with its value, in order, but only in the text as written: a
        /// value written in is never searched again, nor is the text around it read across it.
        /// Then writes each of <paramref name="thenAt"/> at its own position, where the text there
        /// hasn't been taken.
        /// </summary>
        private static string ReplaceInTextAsWritten(
            this string text,
            IEnumerable<(string Text, string Value)> replacements,
            StringComparison comparison,
            IEnumerable<(int Start, int Length, string Value)>? thenAt = null)
        {
            // The values are written only at the end. Until then, an occurrence that overlaps text an
            // earlier pair has taken is skipped, as if that text were already gone.
            bool[]? taken = null;
            var written = new List<(int Start, int Length, string Value)>();
            bool Take(int start, int length, string value)
            {
                if (taken != null)
                    for (var i = start; i < start + length; i++)
                        if (taken[i]) return false;
                taken ??= new bool[text.Length];
                for (var i = start; i < start + length; i++) taken[i] = true;
                written.Add((start, length, value ?? ""));
                return true;
            }
            foreach (var (search, value) in replacements)
            {
                if (string.IsNullOrEmpty(search)) continue;
                var from = 0;
                int at;
                while (from <= text.Length - search.Length
                    && (at = text.IndexOf(search, from, comparison)) >= 0)
                    from = Take(at, search.Length, value) ? at + search.Length : at + 1;
            }
            foreach (var (start, length, value) in thenAt ?? Enumerable.Empty<(int, int, string)>())
                if (length > 0) Take(start, length, value);
            if (written.Count == 0) return text;

            written.Sort((x, y) => x.Start.CompareTo(y.Start));
            var result = new StringBuilder(text.Length);
            var copied = 0;
            foreach (var (start, length, value) in written)
            {
                result.Append(text, copied, start - copied).Append(value);
                copied = start + length;
            }
            return result.Append(text, copied, text.Length - copied).ToString();
        }

        /// <summary>
        /// Removes the specified number of characters from the end of a string.
        /// </summary>
        /// <param name="src">The source string</param>
        /// <param name="charCount">Number of characters to remove from the end. If null, returns the string unchanged.</param>
        /// <returns>The string with characters removed from the end, or the original string if charCount is null or the string is too short</returns>
        public static string? RemoveLast(this string src, int? charCount = null)
            => (charCount == null || src == null || src.Length<1)?src
                : src.Remove(src.Length - (int)charCount);

    }
}
