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

        /// <summary>
        /// Serializes an object to JSON text asynchronously and in a streaming (deferred) manner
        /// </summary>
        /// <param name="data">The object to serialize</param>
        /// <param name="outputStream">The output stream to write the JSON text to</param>
        /// <param name="options">The optional JSON serializer options</param>
        /// <param name="cancellationToken">The optional cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <exception cref="ArgumentNullException">Thrown when the outputStream is null</exception>
        public static async Task JsonSerializeAsync(
            this object data,
            Stream outputStream,
            JsonSerializerOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (outputStream == null)
            {
                throw new ArgumentNullException(nameof(outputStream));
            }
            using (var writer = new Utf8JsonWriter(outputStream))
            {
                await writer.SerializeAsync(data);
            }
        }


        private static async Task SerializeAsync(
            this Utf8JsonWriter writer,
            object? value,
            JsonSerializerOptions? options = null,
            CancellationToken cancellationToken = default
            )
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            if (value is IAsyncEnumerable<object> asyncEnumerable)
            {
                writer.WriteStartArray();
                await foreach (var item in asyncEnumerable)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    await SerializeAsync(writer, item, options, cancellationToken);
                }
                writer.WriteEndArray();
            }
            else if (value is IEnumerable<object> enumerable)
            {
                writer.WriteStartArray();
                foreach (var item in enumerable)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    await SerializeAsync(writer, item, options, cancellationToken);
                }
                writer.WriteEndArray();
            }
            else if (value != null && !value.IsJsonPrimitive())
            {
                writer.WriteStartObject();
                foreach (var property in value.GetType().GetProperties())
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    writer.WritePropertyName(property.Name);
                    var propertyValue = property.GetValue(value);
                    await SerializeAsync(writer, propertyValue, options, cancellationToken);
                }
                writer.WriteEndObject();
            }
            else
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                JsonSerializer.Serialize(writer, value, options);
            }
        }

        public static bool IsJsonPrimitive(this object value)
        {
            return value is string || value.GetType().IsPrimitive || value is decimal || value is DateTime || value is Guid;
        }


        //public static async Task DeferredWriteAsJsonAsync(this HttpResponse response, ObjectResult result)
        //{
        //    response.StatusCode = result.StatusCode ?? 200;
        //    response.ContentType = "application/json";

        //    await using (var writer = new Utf8JsonWriter(response.BodyWriter))
        //    {
        //        await JsonSerializeAsync(writer, result.Value);
        //    }
        //}
    }
}
