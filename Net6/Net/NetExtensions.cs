using Com.H.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Com.H.Net
{
    public static class NetExtensions
    {
        public static async Task<byte[]> GetByteArrayAsync(
            this Uri uri,
            CancellationToken? cToken = null,
            string? referer = null,
            string? userAgent = null,
            string? contentType = null
            )
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            if (!uri.IsWellFormedOriginalString()) throw new FormatException($"Invalid {nameof(uri)} format");
            HttpClient client = new();
            if (!string.IsNullOrWhiteSpace(referer))
                client.DefaultRequestHeaders.Add("Referer", referer);

            if (!string.IsNullOrWhiteSpace(userAgent))
                client.DefaultRequestHeaders.Add("User-Agent", userAgent);
            if (!string.IsNullOrWhiteSpace(contentType))
                client.DefaultRequestHeaders.Add("Content-Type", contentType);

            client.DefaultRequestHeaders.Add("Connection", "keep-alive");

            return cToken != null ? await client.GetByteArrayAsync(uri, (CancellationToken)cToken)
                : await client.GetByteArrayAsync(uri);
            
        }

        public static async Task<string> GetAsync(
            this Uri uri,
            CancellationToken? cToken = null,
            string? referer = null,
            string? userAgent = null,
            string? contentType = null
            )
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            if (!uri.IsWellFormedOriginalString()) throw new FormatException($"Invalid {nameof(uri)} format");
            if (uri.Scheme?.EqualsIgnoreCase("file") == true)
            {
                if (!File.Exists(uri.LocalPath)) throw new FileNotFoundException($"File not found: {uri.LocalPath}", uri.LocalPath);
                return cToken is null? await File.ReadAllTextAsync(uri.LocalPath)
                    :
                    await File.ReadAllTextAsync(uri.LocalPath, (CancellationToken)cToken);
            }
            HttpClient client = new();
            if (!string.IsNullOrWhiteSpace(referer))
                client.DefaultRequestHeaders.Add("Referer", referer);

            if (!string.IsNullOrWhiteSpace(userAgent))
                client.DefaultRequestHeaders.Add("User-Agent", userAgent);
            if (!string.IsNullOrWhiteSpace(contentType))
                client.DefaultRequestHeaders.Add("Content-Type", contentType);

            client.DefaultRequestHeaders.Add("Connection", "keep-alive");
            var response = cToken != null ? await client.GetAsync(uri, (CancellationToken)cToken)
                : await client.GetAsync(uri);
            _ = response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();

        }

        public static string Get(
            this Uri uri,
            string? referer = null,
            string? userAgent = null,
            string? contentType = null

            )
            => GetAsync(uri, null, referer, userAgent, contentType).GetAwaiter().GetResult();


        public static byte[] GetByteArray(
            this Uri uri,
            string? referer = null,
            string? userAgent = null,
            string? contentType = null

            )
            => GetByteArrayAsync(uri, null, referer, userAgent, contentType).GetAwaiter().GetResult();


        public static Uri? GetParentUri(this Uri uri)
        {
            if (uri == null || uri.AbsoluteUri == null) return null;
            var uriPath = uri.AbsoluteUri.EndsWith("/") ?
                uri.AbsoluteUri.Remove(uri.AbsoluteUri.Length - 1) : uri.AbsoluteUri;
            var lastIndexOfSeperator = uriPath.LastIndexOf("/");
            if (lastIndexOfSeperator > -1)
                return new Uri(uriPath[..(lastIndexOfSeperator + 1)], UriKind.Absolute);
            return new Uri(uri.AbsoluteUri, UriKind.Absolute);
        }

        //public static string GetParentUriString(this string uriStr)
        //{
        //    if (string.IsNullOrWhiteSpace(uriStr)) return null;
        //    uriStr = uriStr.EndsWith("/") ?
        //        uriStr.RemoveLast(1): uriStr;
            
        //    var lastIndexOfSeperator = uriStr.LastIndexOf("/");
        //    if (lastIndexOfSeperator > -1)
        //        return uriStr.Substring(0, lastIndexOfSeperator + 1);
        //    return uriStr;

        //}

    }
}
