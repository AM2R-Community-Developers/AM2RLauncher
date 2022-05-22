using System.Runtime.InteropServices;

namespace AM2RLauncherLib;

/// <summary>
/// Class that has information about the current running operating system.
/// </summary>
public static class OS
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
    public static readonly string Name = DetermineOsName();

    /// <summary>
    /// Generates a string representation of the current OS
    /// </summary>
    private static string DetermineOsName()
    {
        if (IsWindows)
            return "Windows";
        if (IsLinux)
            return "Linux";
        if (IsMac)
            return "Mac";

        return "Unknown OS";
    }

    /// <summary>
    /// Checks if this is run via WINE.
    /// </summary>
    public static readonly bool IsThisRunningFromWine = CheckIfRunFromWINE();

    /// <summary>
    /// Checks if the Launcher is ran from WINE.
    /// </summary>
    /// <returns><see langword="true"/> if run from WINE, <see langword="false"/> if not.</returns>
    private static bool CheckIfRunFromWINE()
    {
        // We check for wine by seeing if a reg entry exists.
        // Not the best way, and could be removed from the future, but good enough for our purposes.
        if (IsWindows && (Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Wine") != null))
            return true;

        return false;
    }
}