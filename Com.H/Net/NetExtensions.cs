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
        public static string GetContent(
            this Uri uri,
            string referer = null,
            string userAgent = null
            )
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            if (!uri.IsWellFormedOriginalString()) throw new FormatException($"Invalid {nameof(uri)} format");
            WebRequest req;
            if (!string.IsNullOrEmpty(referer) || !string.IsNullOrEmpty(userAgent))
            {
                req = (HttpWebRequest)WebRequest.Create(uri);
                ((HttpWebRequest)req).Referer = referer;
                ((HttpWebRequest)req).UserAgent = userAgent;
            }
            else req = WebRequest.Create(uri);
            using var reader = new StreamReader(req.GetResponse().GetResponseStream());
            return reader.ReadToEnd();
        }

        public static Task<string> GetContentAsync(
            this Uri uri,
            CancellationToken? token = null,
            string referer = null,
            string userAgent = null
            )
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            if (!uri.IsWellFormedOriginalString()) throw new FormatException($"Invalid {nameof(uri)} format");
            WebRequest req;
            if (!string.IsNullOrEmpty(referer) || !string.IsNullOrEmpty(userAgent))
            {
                req = (HttpWebRequest)WebRequest.Create(uri);
                ((HttpWebRequest)req).Referer = referer;
                ((HttpWebRequest)req).UserAgent = userAgent;
            }
            else req = WebRequest.Create(uri);
            return Task.Run<string>(() =>
            {
                var resp = req.GetResponseAsync();
                if (token != null)
                    resp.Wait((CancellationToken)token);
                else resp.Wait();
                if (!resp.IsCompleted) return null;
                using var r = new StreamReader(resp.GetAwaiter().GetResult().GetResponseStream());
                var content = r.ReadToEndAsync();
                if (token != null)
                    content.Wait((CancellationToken)token);
                else content.Wait();
                if (!content.IsCompleted) return null;
                return content.GetAwaiter().GetResult();
            });
            
        }

        public static Uri GetParentUri(this Uri uri)
        {
            if (uri == null || uri.AbsoluteUri == null) return null;
            var uriPath = uri.AbsoluteUri.EndsWith("/") ?
                uri.AbsoluteUri.Remove(uri.AbsoluteUri.Length - 1) : uri.AbsoluteUri;
            var lastIndexOfSeperator = uriPath.LastIndexOf("/");
            if (lastIndexOfSeperator > -1)
                return new Uri(uriPath.Substring(0, lastIndexOfSeperator + 1), UriKind.Absolute);
            return new Uri(uri.AbsoluteUri, UriKind.Absolute);
        }

    }
}
