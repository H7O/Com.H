using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.H.Net.Mail
{
    public static class MailExtensions
    {

        private const string MatchEmailPattern = @"^(([\w-]+\.)+[\w-]+|([a-zA-Z]{1}|[\w-]{2,}))@"
        + @"((([0-1]?[0-9]{1,2}|25[0-5]|2[0-4][0-9])\.([0-1]?[0-9]{1,2}|25[0-5]|2[0-4][0-9])\."
       + @"([0-1]?[0-9]{1,2}|25[0-5]|2[0-4][0-9])\.([0-1]?[0-9]{1,2}|25[0-5]|2[0-4][0-9])){1}|"
       + @"([a-zA-Z]+[\w-]+\.)+[a-zA-Z]{2,4})$";

        public static bool IsEmail(this string email) 
        {
            if (!string.IsNullOrEmpty(email)) return System.Text.RegularExpressions.Regex.IsMatch(email, MatchEmailPattern);
            else return false;
        }
    }
}
