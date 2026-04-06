using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Com.H.Runtime.InteropServices
{
    public static class InteropExt
    {
        public static OSPlatform? CurrentOSPlatform
            => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OSPlatform.Windows
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? OSPlatform.Linux
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? OSPlatform.OSX
                : default;

        
    }
}
