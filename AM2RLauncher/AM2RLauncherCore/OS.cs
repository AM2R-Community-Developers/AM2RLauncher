using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace AM2RLauncher.Core
{
    public class OS
    {
        public static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public static readonly bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        public static readonly bool IsMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public static readonly bool IsUnix = IsLinux || IsMac;

        public static readonly string Name = DetermineOSName();

        private static string DetermineOSName()
        {
            if (IsWindows)
                return "Windows";
            else if (IsLinux)
                return "Linux";
            else if (IsMac)
                return "MacOS";
            else
                return "Unknown OS";
        }
    }
}
