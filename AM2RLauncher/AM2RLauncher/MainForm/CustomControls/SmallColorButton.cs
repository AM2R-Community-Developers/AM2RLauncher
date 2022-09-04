using Eto.Drawing;

namespace AM2RLauncher;

/// <summary>
/// A custom button implementation for the AM2RLauncher.
/// Generates a small <see cref="ColorButton"/> with AM2R colors.
/// </summary>
public class SmallColorButton : ColorButton
{
    private Font smallButtonFont = new Font(SystemFont.Default, 10);
    
    /// <summary>
    /// Initializes a new <see cref="SmallColorButton"/>.
    /// </summary>
    /// <param name="text">The text that should be displayed on the button.</param>
    public SmallColorButton(string text)
    {
        Font = smallButtonFont;
        Text = text;
        Height = 40;
        Width = 275;
        TextColor = LauncherColors.Green;
        TextColorDisabled = LauncherColors.Inactive;
        BackgroundColor = LauncherColors.BG;
        BackgroundColorHover = LauncherColors.BGHover;
        FrameColor = LauncherColors.Green;
        FrameColorDisabled = LauncherColors.Inactive;
    }
}