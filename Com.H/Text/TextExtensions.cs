using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

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
            => originalString.IsNullEqual(stringToCompare)?? 
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
    }
}
