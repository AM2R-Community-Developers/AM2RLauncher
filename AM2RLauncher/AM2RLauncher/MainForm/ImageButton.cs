using System;
using System.Collections.Generic;
using System.Text;
using Eto.Drawing;

namespace Pablo.Controls
{
	/// <summary>
	/// Extension of <see cref="CustomButton"/> that allows image assignment and drawing.
	/// Originally written by cwensley (https://gist.github.com/cwensley/95000998e37acd93e830),
	/// modified by Lojemiru and Miepee for use in the AM2RLauncher.
	/// </summary>
	public class ImageButton : CustomButton
	{
		/// <summary>
		/// Whether or not this control's size has been set.
		/// </summary>
		bool sizeSet;

		/// <summary>
		/// Gets or sets the <see cref="Eto.Drawing.Image"/> to draw during <see cref="OnPaint"/>.
		/// </summary>
		public Image Image { get; set; }

		/// <summary>
		/// Gets or sets the <see cref="Eto.Drawing.Image"/> to draw during <see cref="OnPaint"/> if this control is disabled.
		/// </summary>
		public Image DisabledImage { get; set; }

		/// <summary>
		/// Gets or sets the <see cref="Eto.Drawing.Size"/> of this control.
		/// </summary>
		public override Size Size
		{
			get { return base.Size; }
			set { base.Size = value; sizeSet = true; }
		}

		/// <summary>
		/// Default contructor, assigns no special attributes.
		/// </summary>
		public ImageButton() { }

		/// <summary>
		/// Event raised when this control is loading.
		/// </summary>
		/// <param name="e"></param>
		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			if (Image != null)
			{
				if (!sizeSet)
					this.Size = Image.Size + 4;
			}
		}

		/// <summary>
		/// Event raised when this control has finished loading.
		/// </summary>
		/// <param name="e"></param>
		protected override void OnLoadComplete(EventArgs e)
		{
			base.OnLoadComplete(e);

			if (DisabledImage == null)
			{
				var image = Image;
				if (image != null)
				{
					var disabledImage = new Bitmap(image.Size.Width, image.Size.Height, PixelFormat.Format32bppRgba);
					using (var graphics = new Graphics(disabledImage))
					{
						graphics.DrawImage(image, 0, 0);
					}
					using (var bd = disabledImage.Lock())
					{
						unsafe
						{
							var data = (int*)bd.Data;
							for (int i = 0; i < bd.ScanWidth * disabledImage.Size.Height; i++)
							{
								var col = Color.FromArgb(bd.TranslateDataToArgb(*data));
								var gray = (col.R + col.G + col.B) / 3;
								*data = bd.TranslateArgbToData(Color.FromGrayscale(gray, 0.8f).ToArgb());
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Event raised to draw this control.
		/// </summary>
		/// <param name="pe"></param>
		protected override void OnPaint(Eto.Forms.PaintEventArgs pe)
		{
			var image = this.Enabled ? Image : DisabledImage;
			var size = image.Size.FitTo(this.Size - 2);
			var xoffset = (this.Size.Width - size.Width) / 2;
			var yoffset = (this.Size.Height - size.Height) / 2;
			pe.Graphics.DrawImage(image, xoffset, yoffset, size.Width, size.Height);
		}
	}

}
