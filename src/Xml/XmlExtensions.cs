using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Com.H.Xml
{
    public static class XmlExtensions
    {
        public static string? RemoveInvalidXmlCharacters(this string text)
            => text == null ? null
            : System.Text.RegularExpressions.Regex.Replace(text, @"[\x1A|\x1F]", "");



        /// <summary>
        /// Serializes an object to XML asynchronously and in a streaming (deferred) manner.
        /// </summary>
        /// <param name="data">The object to serialize.</param>
        /// <param name="outputStream">The output stream to write the XML to.</param>
        /// <param name="rootElementName">Optional root element name.</param>
        /// <param name="settings">Optional XML writer settings.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the output stream is null.</exception>
        public static async Task XmlSerializeAsync(
            this object data,
            Stream outputStream,
            string? rootElementName = null,
            XmlWriterSettings? settings = null,
            CancellationToken cancellationToken = default)
        {
            if (outputStream == null)
            {
                throw new ArgumentNullException(nameof(outputStream));
            }

            settings ??= new XmlWriterSettings { Async = true, Indent = true };

            using (var writer = XmlWriter.Create(outputStream, settings))
            {
                await writer.WriteStartDocumentAsync();
                await writer.SerializeAsync(data, rootElementName ?? "root", cancellationToken);
                await writer.WriteEndDocumentAsync();
                await writer.FlushAsync();
            }
        }

        /// <summary>
        /// Serializes an object to XML asynchronously using an XmlWriter.
        /// </summary>
        /// <param name="data">The object to serialize.</param>
        /// <param name="writer">The XmlWriter to write the XML to.</param>
        /// <param name="rootElementName">Optional root element name.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the writer is null.</exception>
        public static async Task XmlSerializeAsync(
            this object data,
            XmlWriter writer,
            string? rootElementName = null,
            CancellationToken cancellationToken = default)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            await writer.WriteStartDocumentAsync();
            await writer.SerializeAsync(data, rootElementName ?? "root", cancellationToken);
            await writer.WriteEndDocumentAsync();
            await writer.FlushAsync();
        }

        /// <summary>
        /// Asynchronously serializes an object to XML using the provided XmlWriter.
        /// </summary>
        /// <param name="writer">The XmlWriter to write the XML to.</param>
        /// <param name="value">The object to serialize.</param>
        /// <param name="elementName">The name of the XML element.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public static async Task SerializeAsync(
            this XmlWriter writer,
            object? value,
            string elementName,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            elementName = SanitizeXmlElementName(elementName);

            if (value is IAsyncEnumerable<object> asyncEnumerable)
            {
                await writer.WriteStartElementAsync(null, elementName, null);

                await foreach (var item in asyncEnumerable.WithCancellation(cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    await writer.SerializeAsync(item, "Item", cancellationToken);
                }

                await writer.WriteEndElementAsync();
            }
            else if (value is IEnumerable<object> enumerable)
            {
                await writer.WriteStartElementAsync(null, elementName, null);

                foreach (var item in enumerable)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    await writer.SerializeAsync(item, "Item", cancellationToken);
                }

                await writer.WriteEndElementAsync();
            }
            else if (value != null && !value.IsXmlPrimitive())
            {
                await writer.WriteStartElementAsync(null, elementName, null);

                foreach (var property in value.GetType().GetProperties())
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var propertyValue = property.GetValue(value);
                    var propertyName = property.Name;

                    await writer.SerializeAsync(propertyValue, propertyName, cancellationToken);
                }

                await writer.WriteEndElementAsync();
            }
            else
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await writer.WriteStartElementAsync(null, elementName, null);

                if (value != null)
                {
                    var valueString = value.ToString();
                    await writer.WriteStringAsync(valueString);
                }

                await writer.WriteEndElementAsync();
            }
        }

        /// <summary>
        /// Determines if the object is a primitive type suitable for XML content.
        /// </summary>
        /// <param name="value">The object to check.</param>
        /// <returns>True if the object is a primitive type; otherwise, false.</returns>
        public static bool IsXmlPrimitive(this object value)
        {
            return value is string ||
                   value.GetType().IsPrimitive ||
                   value is decimal ||
                   value is DateTime ||
                   value is Guid;
        }

        /// <summary>
        /// Sanitizes an XML element name to ensure it is valid.
        /// </summary>
        /// <param name="name">The element name to sanitize.</param>
        /// <returns>A valid XML element name.</returns>
        public static string SanitizeXmlElementName(string name)
        {
            return XmlConvert.EncodeLocalName(name);
        }

        // Sample usage in a controller or middleware
        // public static async Task DeferredWriteAsXmlAsync(
        //     this HttpResponse response,
        //     ObjectResult result,
        //     CancellationToken cancellationToken = default)
        // {
        //     if (result.Value == null)
        //     {
        //         response.StatusCode = result.StatusCode ?? 204;
        //         return;
        //     }
        //
        //     response.StatusCode = result.StatusCode ?? 200;
        //     response.ContentType = "application/xml";
        //
        //     await result.Value.XmlSerializeAsync(response.Body, rootElementName: "root", cancellationToken: cancellationToken);
        // }


    }
}
