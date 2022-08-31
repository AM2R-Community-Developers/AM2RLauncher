namespace AM2RLauncher;

public class BigColorButton : ColorButton
{
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