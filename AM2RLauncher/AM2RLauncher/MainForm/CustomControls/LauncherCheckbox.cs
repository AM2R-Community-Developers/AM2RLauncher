using Eto.Forms;

namespace AM2RLauncher;

/// <summary>
/// A custom AM2RLauncher-themed checkbox.
/// </summary>
public class LauncherCheckbox : CheckBox
{
    /// <summary>
    /// Initializes a new <see cref="LauncherCheckbox"/>.
    /// </summary>
    /// <param name="text">The text that should be displayed next to the checkbox.</param>
    /// <param name="checkedState">The checked state of the checkbox.</param>
    public LauncherCheckbox(string text, bool? checkedState = false)
    {
        Text = text;
        Checked = checkedState;
        TextColor = LauncherColors.Green;
    }
}