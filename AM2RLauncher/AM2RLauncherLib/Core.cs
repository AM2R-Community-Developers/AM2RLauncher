using log4net;
using System;
using System.Runtime.InteropServices;

namespace AM2RLauncherLib;

/// <summary>
/// Class that has core stuff that doesn't fit anywhere else.
/// </summary>
public static class Core
{
    /// <summary>
    /// Our log object, that handles logging the current execution to a file.
    /// </summary>
    public static readonly ILog Log = LogManager.GetLogger(typeof(Core));

    /// <summary>The Version that identifies this current release.</summary>
    public const string Version = "2.2.0";

    /// <summary>
    /// Indicates whether or not we have established an internet connection.
    /// </summary>
    public static readonly bool IsInternetThere = HelperMethods.IsConnectedToInternet();

    /// <summary>
    /// Path where the Launcher's PatchData folder is located.
    /// </summary>
    public static readonly string PatchDataPath = CrossPlatformOperations.CurrentPath + "/PatchData";

    /// <summary>
    /// Path where the AM2R_11.zip is located.
    /// </summary>
    public static readonly string AM2R11File = CrossPlatformOperations.CurrentPath + "/AM2R_11.zip";

    /// <summary>
    /// Path where the Launcher's Profiles folder is located.
    /// </summary>
    public static readonly string ProfilesPath = CrossPlatformOperations.CurrentPath + "/Profiles";

    /// <summary>
    /// Path where the Launcher's Mods folder is located.
    /// </summary>
    public static readonly string ModsPath = CrossPlatformOperations.CurrentPath + "/Mods";

    /// <summary>
    /// This is used on Windows only. This sets a window to be in foreground. It's used i.e. to fix AM2R just being hidden.
    /// </summary>
    /// <param name="hWnd">Pointer to the process you want to have in the foreground.</param>
    /// <returns></returns>
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
}