using AM2RLauncherLib;
using Eto.Drawing;

namespace AM2RLauncher;

public static class LauncherColors
{
    // Colors
    /// <summary>The main green color.</summary>
    public static readonly Color Green = Color.FromArgb(142, 188, 35);
    /// <summary>The warning red color.</summary>
    public static readonly Color Red = Color.FromArgb(188, 10, 35);
    /// <summary>The main inactive color.</summary>
    public static readonly Color Inactive = Color.FromArgb(109, 109, 109);
    /// <summary>The black background color without alpha value.</summary>
    public static readonly Color BGNoAlpha = Color.FromArgb(10, 10, 10);
    /// <summary>The black background color.</summary>
    // XORG can't display alpha anyway, and Wayland breaks with it.
    // TODO: that sounds like an Eto issue. investigate, try to open eto issue.
    public static readonly Color BG = OS.IsLinux ? Color.FromArgb(10, 10, 10) : Color.FromArgb(10, 10, 10, 80);
    /// <summary>The lighter green color on hover.</summary>
    public static readonly Color BGHover = Color.FromArgb(17, 28, 13);
}