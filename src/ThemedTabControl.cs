using System.Drawing;
using System;
using System.Windows.Forms;

namespace NavimowDesktopController
{
    internal sealed class ThemedTabControl : TabControl
    {
        private const int WmPaint = 0x000F;
        private const int WmEraseBkgnd = 0x0014;
        private const int BorderThickness = 2;

        public Color HeaderBackgroundColor { get; set; }
        public Color HeaderBorderColor { get; set; }
        public Color PageBorderColor { get; set; }

        public ThemedTabControl()
        {
            this.HeaderBackgroundColor = SystemColors.Control;
            this.HeaderBorderColor = SystemColors.ControlDark;
            this.PageBorderColor = SystemColors.ControlDark;
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WmPaint || m.Msg == WmEraseBkgnd)
            {
                this.PaintHeaderRemainder();
                this.PaintTabOutlines();
                this.PaintContentBorder();
            }
        }

        private void PaintHeaderRemainder()
        {
            if (!this.IsHandleCreated || this.TabCount <= 0)
            {
                return;
            }

            Rectangle lastTabBounds;
            Rectangle firstTabBounds;

            try
            {
                firstTabBounds = this.GetTabRect(0);
                lastTabBounds = this.GetTabRect(this.TabCount - 1);
            }
            catch
            {
                return;
            }

            var headerHeight = firstTabBounds.Height + 4;
            var fillX = lastTabBounds.Right;
            var fillWidth = this.ClientSize.Width - fillX;
            if (fillWidth <= 0)
            {
                return;
            }

            using (var graphics = this.CreateGraphics())
            using (var backgroundBrush = new SolidBrush(this.HeaderBackgroundColor))
            using (var borderBrush = new SolidBrush(this.HeaderBorderColor))
            {
                var fillBounds = new Rectangle(fillX, 0, fillWidth, headerHeight);
                graphics.FillRectangle(backgroundBrush, fillBounds);
                graphics.FillRectangle(borderBrush, fillBounds.X, fillBounds.Bottom - BorderThickness, fillBounds.Width, BorderThickness);
                graphics.FillRectangle(borderBrush, fillBounds.Right - BorderThickness, fillBounds.Y, BorderThickness, fillBounds.Height);
            }
        }

        private void PaintContentBorder()
        {
            if (!this.IsHandleCreated || this.TabCount <= 0)
            {
                return;
            }

            Rectangle firstTabBounds;
            try
            {
                firstTabBounds = this.GetTabRect(0);
            }
            catch
            {
                return;
            }

            var top = Math.Max(0, firstTabBounds.Bottom - 1);
            var left = 0;
            var right = this.ClientSize.Width - 1;
            var bottom = this.ClientSize.Height - 1;
            if (right <= left || bottom <= top)
            {
                return;
            }

            using (var graphics = this.CreateGraphics())
            using (var borderBrush = new SolidBrush(this.PageBorderColor))
            {
                graphics.FillRectangle(borderBrush, left, top, BorderThickness, bottom - top + 1);
                graphics.FillRectangle(borderBrush, right - BorderThickness + 1, top, BorderThickness, bottom - top + 1);
                graphics.FillRectangle(borderBrush, left, bottom - BorderThickness + 1, right - left + 1, BorderThickness);
            }
        }

        private void PaintTabOutlines()
        {
            if (!this.IsHandleCreated || this.TabCount <= 0)
            {
                return;
            }

            using (var graphics = this.CreateGraphics())
            using (var borderBrush = new SolidBrush(this.HeaderBorderColor))
            {
                for (int index = 0; index < this.TabCount; index++)
                {
                    Rectangle bounds;
                    try
                    {
                        bounds = this.GetTabRect(index);
                    }
                    catch
                    {
                        continue;
                    }

                    if (bounds.Width <= 0 || bounds.Height <= 0)
                    {
                        continue;
                    }

                    graphics.FillRectangle(borderBrush, bounds.X, bounds.Y, bounds.Width, BorderThickness);
                    graphics.FillRectangle(borderBrush, bounds.X, bounds.Y, BorderThickness, bounds.Height);
                    graphics.FillRectangle(borderBrush, bounds.Right - BorderThickness, bounds.Y, BorderThickness, bounds.Height);
                    graphics.FillRectangle(borderBrush, bounds.X, bounds.Bottom - BorderThickness, bounds.Width, BorderThickness);
                }
            }
        }
    }
}
