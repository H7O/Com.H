using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

namespace Com.H.Security.Cryptography
{
    /// <summary>
    /// Provides extension methods for cryptographic hash operations.
    /// </summary>
    public static class CryptographyExtensions
    {
        /// <summary>
        /// Computes the SHA256 hash of a string and returns it as a Base64 encoded string.
        /// </summary>
        /// <param name="text">The text to hash</param>
        /// <returns>SHA256 hash as Base64 string</returns>
        public static string ToSha256InBase64String(this string text)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        /// <summary>
        /// Computes the SHA256 hash of a string and returns it as a hexadecimal string.
        /// </summary>
        /// <param name="text">The text to hash</param>
        /// <returns>SHA256 hash as hexadecimal string</returns>
        public static string ToSha256Hash(this string text)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
        }

        /// <summary>
        /// Computes the MD5 hash of a string and returns it as a Base64 encoded string.
        /// </summary>
        /// <param name="text">The text to hash</param>
        /// <returns>MD5 hash as Base64 string</returns>
        public static string ToMD5InBase64String(this string text)
        {
            using var md5 = MD5.Create();
            return Convert.ToBase64String(md5.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        /// <summary>
        /// Computes the MD5 hash of a string and returns it as a hexadecimal string.
        /// </summary>
        /// <param name="text">The text to hash</param>
        /// <returns>MD5 hash as hexadecimal string</returns>
        public static string ToMD5Hash(this string text)
        {
            using var md5 = MD5.Create();
            return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
        }
    }
}
