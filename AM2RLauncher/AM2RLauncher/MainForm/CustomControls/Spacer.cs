using Eto.Forms;

namespace AM2RLauncher;

/// <summary>
/// A custom control that acts as a spacer between other controls.
/// </summary>
public class Spacer : Label
{
    /// <summary>
    /// Initialize a new <see cref="Spacer"/>.
    /// </summary>
    /// <param name="height">The height of the spacer in pixel.</param>
    public Spacer(int height)
    {
        Height = height;
    }
}