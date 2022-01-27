using log4net;
using System;
using System.Runtime.InteropServices;

namespace AM2RLauncher.Core;

/// <summary>
/// Class that has core stuff that doesn't fit anywhere else
/// </summary>
public static class Core
{
    /// <summary>
    /// Our log object, that handles logging the current execution to a file.
    /// </summary>
    public static readonly ILog Log = LogManager.GetLogger(typeof(Core));

    /// <summary>The Version that identifies this current release.</summary>
    public const string VERSION = "2.2.0";

    /// <summary>
    /// Checks if this is run via WINE.
    /// </summary>
    public static readonly bool IsThisRunningFromWine = CheckIfRunFromWINE();

    /// <summary>
    /// Indicates whether or not we have established an internet connection.
    /// </summary>
    public static readonly bool IsInternetThere = HelperMethods.IsConnectedToInternet();

    /// <summary>
    /// Checks if this is ran from WINE
    /// </summary>
    /// <returns><see langword="true"/> if run from WINE, <see langword="false"/> if not.</returns>
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