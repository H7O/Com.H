using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

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
            using (var reader = new StreamReader(req.GetResponse().GetResponseStream()))
                return reader.ReadToEnd();
        }
    }
}
