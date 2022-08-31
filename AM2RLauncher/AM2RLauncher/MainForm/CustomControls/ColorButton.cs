using Eto.Drawing;
using Eto.Forms;
using Pablo.Controls;

namespace AM2RLauncher;

/// <summary>
/// Extension of <see cref="CustomButton"/> that allows for advanced color settings.
/// </summary>
public class ColorButton : CustomButton
{
    /// <summary>The <see cref="Color"/> to draw the background with when <see cref="CustomButton.Hover"/> is true.</summary>
    public Color BackgroundColorHover { get; set; }

    /// <summary>The <see cref="Font"/> to use for text drawing.</summary>
    public Font Font { get; set; }

    /// <summary>The <see cref="Color"/> with which to draw the frame.</summary>
    public Color FrameColor { get; set; }

    /// <summary>The <see cref="Color"/> with which to draw the frame when it is disabled.</summary>
    public Color FrameColorDisabled { get; set; }

    /// <summary>The text to be drawn.</summary>
    public string Text { get; set; }

    /// <summary>The <see cref="Color"/> with which to draw the <see cref="Text"/>.</summary>
    public Color TextColor { get; set; }

    /// <summary>The <see cref="Color"/> with which to draw the <see cref="Text"/> when it is disabled.</summary>
    public Color TextColorDisabled { get; set; }

    /// <summary>
    /// Creates a <see cref="ColorButton"/> with a default set of attributes.
    /// </summary>
    public ColorButton()
    {
        BackgroundColorHover = Colors.White;
        Font = new Font(SystemFont.Default, 12);
        FrameColor = Colors.White;
        FrameColorDisabled = Colors.Gray;
        Text = "";
        TextColor = Colors.Black;
        TextColorDisabled = Colors.Gray;
    }

    /// <summary>
    /// Event raised to draw this control.
    /// </summary>
    /// <param name="pe"></param>
    protected override void OnPaint(PaintEventArgs pe)
    {
        // Define draw bounds
        Rectangle drawBounds = new Rectangle(2, 2, Width - 5, Height - 5);

        // Draw background
        pe.Graphics.FillRectangle(Hover ? BackgroundColorHover : BackgroundColor, drawBounds);

        // Draw frame
        Pen framePen = new Pen(Enabled ? FrameColor : FrameColorDisabled, 1);
        pe.Graphics.DrawRectangle(framePen, drawBounds);

        SolidBrush brush = new SolidBrush(Enabled ? TextColor : TextColorDisabled);

        // Get text measurements
        SizeF stringSize = pe.Graphics.MeasureString(Font, Text);
        int textWidth = (int)stringSize.Width;
        int textHeight = (int)stringSize.Height;

        // Draw text
        pe.Graphics.DrawText(Font, brush, new PointF(Width / 2 - textWidth / 2, Height / 2 - textHeight / 2), Text);
    }
}