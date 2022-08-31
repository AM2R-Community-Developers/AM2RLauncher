using Eto.Drawing;

namespace AM2RLauncher;

public class SmallColorButton : ColorButton
{
    private Font smallButtonFont = new Font(SystemFont.Default, 10);
    
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