using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace AM2RLauncher.Core
{
    /// <summary>
    /// Class that has information about the current running operating system.
    /// </summary>
    public class OS
    {
        /// <summary>
        /// Determines if the current OS is Windows.
        /// </summary>
        public static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        /// <summary>
        /// Determines if the current OS is Linux.
        /// </summary>
        public static readonly bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        /// <summary>
        /// Determines if the current OS is Mac.
        /// </summary>
        public static readonly bool IsMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        /// <summary>
        /// Determines if the current OS is a unix based system (Mac or Linux).
        /// </summary>
        public static readonly bool IsUnix = IsLinux || IsMac;

        /// <summary>
        /// Gets a string representation of the current OS.
        /// </summary>
        public static readonly string Name = DetermineOSName();

        /// <summary>
        /// Generates a string representation of the current OS
        /// </summary>
        private static string DetermineOSName()
        {
            if (IsWindows)
                return "Windows";
            else if (IsLinux)
                return "Linux";
            else if (IsMac)
                return "Mac";
            else
                return "Unknown OS";
        }
    }
}
