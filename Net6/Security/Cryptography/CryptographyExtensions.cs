﻿using System;
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
    }
}
