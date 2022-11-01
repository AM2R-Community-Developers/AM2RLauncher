using Eto.Drawing;
using Eto.Forms;
using System;

namespace Pablo.Controls;

/// <summary>
/// A custom button implementation for advanced drawing and events.
/// Originally written by cwensley (https://gist.github.com/cwensley/95000998e37acd93e830),
/// modified by Lojemiru and Miepee for use in the AM2RLauncher.
/// </summary>
public class CustomButton : Drawable
{
    /// <summary>Whether or not this <see cref="CustomButton"/> is being pressed.</summary>
    bool pressed;

    /// <summary>Whether or not the mouse is hovering over this <see cref="CustomButton"/>.</summary>
    bool hover;

    /// <summary>Whether or not the mouse is pressed down over this <see cref="CustomButton"/>.</summary>
    bool mouseDown;

    /// <summary>Gets or sets the disabled color of this control.</summary>
    public static Color DisabledColor = Color.FromGrayscale(0.4f, 0.3f);

    /// <summary>Gets or sets the color of this control.</summary>
    public static Color EnabledColor = Colors.Black;

    /// <summary>Gets or sets whether or not this control is enabled.</summary>
    public override bool Enabled
    {
        get { return base.Enabled; }
        set
        {
            if (base.Enabled == value)
                return;
            base.Enabled = value;
            if (Loaded)
                Invalidate();
        }
    }

    /// <summary>Gets or sets <see cref="pressed"/>.</summary>
    public bool Pressed
    {
        get { return pressed; }
        set
        {
            if (pressed != value)
            {
                pressed = value;
                mouseDown = false;
                if (Loaded)
                    Invalidate();
            }
        }
    }

    /// <summary>Gets <see cref="hover"/>.</summary>
    public bool Hover
    {
        get { return hover; }
    }

    /// <summary>Gets either the <see cref="EnabledColor"/> or <see cref="DisabledColor"/> dependent on <see cref="Enabled"/>.</summary>
    public Color DrawColor
    {
        get { return Enabled ? EnabledColor : DisabledColor; }
    }

    /// <summary>Gets or sets whether or not this control should act as a toggle.</summary>
    public bool Toggle { get; set; }

    /// <summary>Gets or sets whether or not this control is persistent.</summary>
    public bool Persistent { get; set; }

    public event EventHandler<EventArgs> Click;

    /// <summary>
    /// Event raised when this control is clicked.
    /// </summary>
    protected virtual void OnClick(EventArgs e)
    {
        if (Click != null)
            Click(this, e);
    }

    /// <summary>Default constructor. Sets <see cref="Enabled"/> to true.</summary>
    public CustomButton()
    {
        Enabled = true;
        CanFocus = true;
    }

    /// <summary>Event raised when this control is resized.</summary>
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Loaded)
            Invalidate();
    }

    /// <summary>
    /// Event raised when the mouse is pressed down over this control's bounding box.
    /// </summary>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Enabled)
            return;
        mouseDown = true;
        Invalidate();
    }

    /// <summary>
    /// Event raised when the mouse enters this control's bounding box.
    /// </summary>
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        Cursor = new Cursor(CursorType.Pointer);
        base.OnMouseEnter(e);
        hover = true;
        Invalidate();
    }

    /// <summary>
    /// Event raised when the mouse leaves this control's bounding box.
    /// </summary>
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        Cursor = new Cursor(CursorType.Default);
        base.OnMouseLeave(e);
        hover = false;
        Invalidate();
    }

    /// <summary>
    /// Event raised, when this control gets focused.
    /// </summary>
    //TODO: change focus via keyboard arrow keys
    protected override void OnGotFocus(EventArgs e)
    {
        hover = true;
        base.OnGotFocus(e);
        Invalidate();
    }

    /// <summary>
    /// Event raised, when this control looses focus.
    /// </summary>
    protected override void OnLostFocus(EventArgs e)
    {
        hover = false;
        base.OnLostFocus(e);
        Invalidate();
    }

    /// <summary>
    /// Event raised, when a key is pressed down.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyData == Keys.Enter && HasFocus)
            OnClick(null);
    }

    /// <summary>
    /// Event raised when the mouse is released over this control's bounding box.
    /// </summary>
    /// <param name="e"></param>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var rect = new Rectangle(this.Size);
        if (mouseDown && rect.Contains((Point)e.Location))
        {
            if (Toggle)
                pressed = !pressed;
            else if (Persistent)
                pressed = true;
            else
                pressed = false;
            mouseDown = false;

            this.Invalidate();
            if (Enabled)
                OnClick(EventArgs.Empty);
        }
        else
        {
            mouseDown = false;
            this.Invalidate();
        }
    }

    /// <summary>
    /// Event raised to draw this control.
    /// </summary>
    /// <param name="pe"></param>
    protected override void OnPaint(PaintEventArgs pe)
    {
        var rect = new Rectangle(this.Size);
        var col = Color.FromGrayscale(hover && Enabled ? 0.95f : 0.8f);
        if (Enabled && (pressed || mouseDown))
        {
            pe.Graphics.FillRectangle(col, rect);
            pe.Graphics.DrawInsetRectangle(Colors.Gray, Colors.White, rect);
        }
        else if (hover && Enabled)
        {
            pe.Graphics.FillRectangle(col, rect);
            pe.Graphics.DrawInsetRectangle(Colors.White, Colors.Gray, rect);
        }

        base.OnPaint(pe);
    }
}