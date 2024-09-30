using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Com.H.Text.Json
{
    public static class JsonExtensions
    {
        /// <summary>
        /// Parses JSON text into dynamic object
        /// </summary>
        /// <param name="jsonText"></param>
        /// <returns></returns>
        public static dynamic ParseJson(this string jsonText)
            => AsDynamic(JsonSerializer.Deserialize<JsonElement>(jsonText))!;

        /// <summary>
        /// Converts a JsonElement to dynamic object
        /// </summary>
        /// <param name="jsonElement"></param>
        /// <returns></returns>
        public static dynamic AsDynamic(this JsonElement jsonElement)
            =>
                jsonElement.ValueKind switch
                {
                    JsonValueKind.Array => jsonElement.EnumerateArray().Select(x => x.AsDynamic()).ToList(),
                    JsonValueKind.Object => jsonElement.EnumerateObject()
                        .Aggregate(new ExpandoObject() as IDictionary<string, object?>,
                        (i, n) =>
                        {
                            i[n.Name] = n.Value.ValueKind switch
                            {
                                JsonValueKind.Array
                                or JsonValueKind.Object => AsDynamic(n.Value),
                                JsonValueKind.String => n.Value.GetString(),
                                JsonValueKind.Number => n.Value.GetDecimal(),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                JsonValueKind.Null => null,
                                _ => throw new ArgumentOutOfRangeException()
                            };
                            return i;
                        }),
                    JsonValueKind.String => jsonElement.GetString()!,
                    JsonValueKind.Number => jsonElement.GetDecimal(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null!,
                    _ => throw new ArgumentOutOfRangeException()
                };
    }
}
