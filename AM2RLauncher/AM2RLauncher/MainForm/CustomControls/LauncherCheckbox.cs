using Eto.Forms;

namespace AM2RLauncher;

public class LauncherCheckbox : CheckBox
{
    public LauncherCheckbox(string text, bool? checkedState = false)
    {
        Text = text;
        Checked = checkedState;
        TextColor = LauncherColors.Green;
    }
}