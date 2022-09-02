using AM2RLauncherLib;
using Eto.Drawing;
using Pablo.Controls;

namespace AM2RLauncher;

public class URLImageButton : ImageButton
{
    public URLImageButton(Bitmap image, string url, string tooltip = "")
    {
        Image = image;
        ToolTip = tooltip;

        Click += (_, _) => CrossPlatformOperations.OpenURL(url);
    }

    public URLImageButton(byte[] image, string url, string tooltip = "") : this(new Bitmap(image), url, tooltip)
    {
        
    }
}