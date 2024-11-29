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
                .Where(m=>m is not null))
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

        private static string DictionaryParameterizedReplace(
            this string text,
            IDictionary<string, object> parameters,
            string? openingMarker = null,
            string? closingMarker = null,
            string? nullValueReplacement = null
            )
        {
            if (string.IsNullOrEmpty(text)
                ||
                (
                    parameters == null
                    ||
                    parameters.Count < 1
                    &&
                    (openingMarker == null || closingMarker == null)
                )
                ) return text;


            var paramList = (openingMarker == null || closingMarker == null) ?
                parameters.Keys.ToList()
                : Regex.Matches(text, openingMarker + @"(?<param>.*?)?" + closingMarker)
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
                    text = item.v == null ?
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
            return
                DictionaryParameterizedReplace(
                    src,
                    Regex.Matches(src, openingMarker + @"(?<param>.*?)?" + closingMarker)
                    .Cast<Match>()
                    .Select(x => x.Groups["param"].Value)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(x => x).Distinct()
                    .ToDictionary(k => k,
                    v => date == null ? DateTime.Now.ToString(v) : 
                    (object)((DateTime)date).ToString(v)),
                openingMarker,
                closingMarker,
                nullValueReplacement
                );
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
                  StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .AsEnumerable();

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
        /// or an IDictionary<string, object> where the IDictionary key is the property name, and the value is the placeholder replacement value
        /// or it could be an IEnumerable<dynamic> where each element of the enumerable could have different properties.
        /// </param>
        /// <param name="openingMarker">placeholder opening marker, e.g. {{</param>
        /// <param name="closingMarker">placeholder closing marker, e.g. }}</param>
        /// <param name="nullValueReplacement">a default value for placeholders that don't have a matching property name in the data model</param>
        /// <returns></returns>

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
                if (openingMarker is not null
                    || closingMarker is not null
                    || nullValueReplacement is not null)
                    return Fill(src, queryParams);
                return Fill(src, queryParams.Select(x => 
                    new QueryParams()
                    {
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

        public static string Fill(
            this string src,
            IEnumerable<QueryParams>? queryParams
            )
        {
            if (queryParams?.Any() == false || string.IsNullOrEmpty(src)) return src;

            Dictionary<string, int> varNameCount = new();

            var paramList = queryParams?.Reverse()
                .SelectMany(x =>
                {
                    var dicParams = x.DataModel?.GetDataModelParameters();
                    return Regex.Matches(src, x.OpenMarker + QueryParams.RegexPattern + x.CloseMarker)
                        .Cast<Match>()
                        .Select(m => m.Groups["param"].Value)
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                        .Distinct()
                        .Select(varName => new
                        {
                            VarName = varName,
                            NestedParamName =
                            $"@vxv_{(varNameCount.ContainsKey(varName) ? ++varNameCount[varName] : varNameCount[varName] = 1) }_{varName}"
                            ,
                            x.OpenMarker,
                            x.CloseMarker,
                            x.NullReplacement,
                            Value = dicParams?.ContainsKey(varName) == true ? dicParams[varName] : null
                        });
                }).ToList();

            if (paramList?.Count > 0)
            {
                foreach (var item in paramList)
                {
                    if (item.Value == null) src = src
                        .Replace(item.OpenMarker + item.VarName + item.CloseMarker,
                            item.NullReplacement, true,
                            CultureInfo.InstalledUICulture);
                    else
                    {
                        src = src
                        .Replace(item.OpenMarker + item.VarName + item.CloseMarker,
                        item.Value.ToString(), true,
                            CultureInfo.InvariantCulture);
                    }
                }

            }
            return src;
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

            foreach (var parameters in parametersEnumerable)
            {
                var paramList = (openingMarker == null || closingMarker == null) ?
                    parameters.Keys.ToList()
                    : Regex.Matches(text, openingMarker + @"(?<param>.*?)?" + closingMarker)
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
                        if (item.v!=null)
                        text = text.Replace(openingMarker + item.k + closingMarker,
                                Convert.ChangeType(item.v, TypeCode.String, CultureInfo.InvariantCulture) as string
                                );
                    }

                }
            }
            return Regex.Replace(text, openingMarker + @"(?<param>.*?)?" + closingMarker, nullValueReplacement??"");
        }

        public static string? RemoveLast(this string src, int? charCount = null)
            => (charCount == null || src == null || src.Length<1)?src
                : src.Remove(src.Length - (int)charCount);

    }
}
