using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Com.H.Xml
{
    public static class XmlExtensions
    {
        public static string? RemoveInvalidXmlCharacters(this string text)
            => text == null ? null
            : System.Text.RegularExpressions.Regex.Replace(text, @"[\x1A|\x1F]", "");
        

    }
}
