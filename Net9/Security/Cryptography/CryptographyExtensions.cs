using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

namespace Com.H.Security.Cryptography
{
    public static class CryptographyExtensions
    {
        public static string ToSha256InBase64String(this string text)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        public static string ToSha256Hash(this string text)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
        }

        public static string ToMD5InBase64String(this string text)
        {
            using var md5 = MD5.Create();
            return Convert.ToBase64String(md5.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        public static string ToMD5Hash(this string text)
        {
            using var md5 = MD5.Create();
            return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
        }
    }
}
