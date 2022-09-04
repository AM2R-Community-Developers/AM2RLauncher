namespace AM2RLauncher;

/// <summary>
/// A custom button implementation for the AM2RLauncher.
/// Generates a big <see cref="ColorButton"/> with AM2R colors.
/// </summary>
public class BigColorButton : ColorButton
{
    /// <summary>
    /// Initializes a <see cref="BigColorButton"/>
    /// </summary>
    /// <param name="text">The text that should be displayed on the button.</param>
    public BigColorButton(string text)
    {
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