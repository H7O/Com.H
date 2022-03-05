using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Com.H.Xml.Linq
{
    public static class XmlLinqExtensions
    {
        /// <summary>
        /// Parses XML string to dynamic data model
        /// </summary>
        /// <param name="xml">XML string</param>
        /// <param name="keepRoot">Determines whether or not to ignore the root element of the XML when constructing the data model. Default is false (i.e. root element is ignored)</param>
        /// <returns></returns>
        public static dynamic ParseXml(this string xml, bool keepRoot = false)
            => AsDynamic(XElement.Parse(xml), keepRoot);


        public static dynamic AsDynamic(this XElement xElement, bool keepRoot = false)
        {
            if (xElement == null) return null;
            if (xElement.HasElements == false && keepRoot == false)
                return Enumerable.Empty<object>();

            if (keepRoot == false)
                return xElement.Elements().Select(x => AsDynamic(x, true)).ToList();

            ExpandoObject obj = new ExpandoObject();

            Dictionary<string, List<string>> properties = new Dictionary<string, List<string>>();
            void AddProperty(string key, string value)
            {
                if (properties.ContainsKey(key))
                    properties[key].Add(value);
                else properties.Add(key, new List<string>() { value });
            }

            if (xElement.HasAttributes == false && xElement.HasElements == false)
                return xElement.Value;
            else
                AddProperty(xElement.Name.LocalName, xElement.Value);


            if (xElement.HasAttributes)
                foreach (var attr in xElement.Attributes())
                    AddProperty(attr.Name.LocalName, attr.Value);

            if (xElement.HasElements)
                foreach (var e in xElement.Elements().Where(x => !x.HasElements && !x.HasAttributes))
                    AddProperty(e.Name.LocalName, e.Value);
            var dic = (IDictionary<string, object>)obj;
            foreach (var item in properties)
                dic[item.Key] = item.Value.Count == 1 ? item.Value[0] : (object)item.Value.ToList();

            foreach (var e in xElement.Elements().Where(x => x.HasAttributes || x.HasElements))
                dic[(properties.ContainsKey(e.Name.LocalName) ? "_" : "") + e.Name.LocalName]
                    = (object)AsDynamic(e, true);

            return obj;
        }
    }
}
