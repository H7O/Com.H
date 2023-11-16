using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Com.H.Net.Ssh
{
    public class SshPath
    {
        public static string Combine(params string[] path)
            => Path.Combine(path).Replace("\\", "/");
    }
}
