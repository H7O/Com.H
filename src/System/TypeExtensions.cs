//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Com.H.System
//{
//    public static class TypeExtensions
//    {
//        public static Uri GetParentUri(this Uri uri)
//        {
//            if (uri == null || uri.AbsoluteUri == null) return null;
//            var uriPath = uri.AbsoluteUri.EndsWith("/") ?
//                uri.AbsoluteUri.Remove(uri.AbsoluteUri.Length - 1) : uri.AbsoluteUri;
//            var lastIndexOfSeperator = uriPath.LastIndexOf("/");
//            if (lastIndexOfSeperator > -1)
//                return new Uri(uriPath.Substring(0, lastIndexOfSeperator + 1), UriKind.Absolute);
//            return new Uri(uri.AbsoluteUri, UriKind.Absolute);
//        }


//    }
//}
