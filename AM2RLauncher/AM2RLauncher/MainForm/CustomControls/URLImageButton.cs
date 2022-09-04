using AM2RLauncherLib;
using Eto.Drawing;
using Pablo.Controls;

namespace AM2RLauncher;

/// <summary>
/// An <see cref="ImageButton"/>, that when clicked opens a URL.
/// </summary>
public class URLImageButton : ImageButton
{
    /// <summary>
    /// Initializes a new <see cref="URLImageButton"/>
    /// </summary>
    /// <param name="image">The image that should be drawn.</param>
    /// <param name="url">The URL that should get opened.</param>
    /// <param name="tooltip">The tool tip for the control.</param>
    public URLImageButton(Bitmap image, string url, string tooltip = "")
    {
        Image = image;
        ToolTip = tooltip;

        Click += (_, _) => CrossPlatformOperations.OpenURL(url);
    }

    /// <summary>
    /// Initializes a new <see cref="URLImageButton"/>
    /// </summary>
    /// <param name="image">The image as a byte array that should be drawn.</param>
    /// <param name="url">The URL that should get opened.</param>
    /// <param name="tooltip">The tool tip for the control.</param>
    public URLImageButton(byte[] image, string url, string tooltip = "") : this(new Bitmap(image), url, tooltip)
    {
        
    }
}