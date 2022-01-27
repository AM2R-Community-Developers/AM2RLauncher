using AM2RLauncher.Core.XML;
using log4net;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AM2RLauncher.Core
{

    //TODO: document a ton of these new movings
    public static class Core
    {
        // Load reference to logger
        /// <summary>
        /// Our log object, that handles logging the current execution to a file.
        /// </summary>
        public static readonly ILog Log = LogManager.GetLogger(typeof(Core));

        /// <summary>The Version that identifies this current release.</summary>
        public const string VERSION = "2.2.0";

        /// <summary>
        /// Checks if this is run via WINE.
        /// </summary>
        public static bool isThisRunningFromWine = CheckIfRunFromWINE();

        /// <summary>
        /// Indicates whether or not we have established an internet connection.
        /// </summary>
        public static readonly bool isInternetThere = HelperMethods.IsConnectedToInternet();

        private static bool CheckIfRunFromWINE()
        { 
            if (OS.IsWindows && Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Wine") != null)
                return true; 
            
            return false;
        }

        /// <summary>
        /// This is used on Windows only. This sets a window to be in foreground, is used i.e. to fix am2r just being hidden.
        /// </summary>
        /// <param name="hWnd">Pointer to the process you want to have in the foreground.</param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
