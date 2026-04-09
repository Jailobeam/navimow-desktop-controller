using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NavimowDesktopController
{
    internal sealed class DarkTextView : Control
    {
        private const int ScrollbarThickness = 14;
        private const int ScrollStepLines = 3;

        private string textContent = string.Empty;
        private string[] lines = new string[0];
        private int verticalOffset;
        private int horizontalOffset;
        private bool verticalDragging;
        private bool horizontalDragging;
        private int verticalDragOffset;
        private int horizontalDragOffset;
        private int contentWidth;
        private int lineHeight;

        public bool StickToBottomOnAppend { get; set; }
        public Color ScrollbarTrackColor { get; set; }
        public Color ScrollbarThumbColor { get; set; }
        public Color BorderColor { get; set; }

        public DarkTextView()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.TabStop = true;
            this.BackColor = Color.FromArgb(35, 41, 44);
            this.ForeColor = Color.FromArgb(236, 241, 238);
            this.ScrollbarTrackColor = Color.FromArgb(29, 35, 38);
            this.ScrollbarThumbColor = Color.FromArgb(49, 72, 64);
            this.BorderColor = Color.FromArgb(43, 49, 53);
            this.Font = new Font("Consolas", 10F);
            this.RecalculateLayout();
        }

        public string TextContent
        {
            get { return this.textContent; }
            set
            {
                this.textContent = value ?? string.Empty;
                this.lines = this.textContent.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                this.RecalculateLayout();
                this.ClampOffsets();
                this.Invalidate();
            }
        }

        public void AppendText(string text)
        {
            var wasAtBottom = this.IsAtBottom();
            this.TextContent = this.textContent + (text ?? string.Empty);
            if (this.StickToBottomOnAppend && wasAtBottom)
            {
                this.ScrollToBottom();
            }
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            this.RecalculateLayout();
            this.ClampOffsets();
            this.Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.ClampOffsets();
            this.Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            var direction = e.Delta > 0 ? -1 : 1;
            this.verticalOffset += direction * ScrollStepLines * this.lineHeight;
            this.ClampOffsets();
            this.Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Rectangle verticalThumb;
            Rectangle horizontalThumb;
            var hasVertical = this.TryGetVerticalThumbBounds(out verticalThumb);
            var hasHorizontal = this.TryGetHorizontalThumbBounds(out horizontalThumb);

            if (hasVertical && verticalThumb.Contains(e.Location))
            {
                this.verticalDragging = true;
                this.verticalDragOffset = e.Y - verticalThumb.Y;
                this.Focus();
                return;
            }

            if (hasHorizontal && horizontalThumb.Contains(e.Location))
            {
                this.horizontalDragging = true;
                this.horizontalDragOffset = e.X - horizontalThumb.X;
                this.Focus();
                return;
            }

            if (hasVertical && e.X >= this.ClientSize.Width - ScrollbarThickness)
            {
                this.verticalOffset += e.Y < verticalThumb.Y ? -this.GetViewportHeight() : this.GetViewportHeight();
                this.ClampOffsets();
                this.Invalidate();
                return;
            }

            if (hasHorizontal && e.Y >= this.ClientSize.Height - ScrollbarThickness)
            {
                this.horizontalOffset += e.X < horizontalThumb.X ? -this.GetViewportWidth() : this.GetViewportWidth();
                this.ClampOffsets();
                this.Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (this.verticalDragging)
            {
                this.UpdateVerticalDrag(e.Y);
            }
            else if (this.horizontalDragging)
            {
                this.UpdateHorizontalDrag(e.X);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            this.verticalDragging = false;
            this.horizontalDragging = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var graphics = e.Graphics;
            graphics.Clear(this.BackColor);

            Rectangle verticalThumb;
            Rectangle horizontalThumb;
            var hasVertical = this.TryGetVerticalThumbBounds(out verticalThumb);
            var hasHorizontal = this.TryGetHorizontalThumbBounds(out horizontalThumb);
            var viewportWidth = this.GetViewportWidth(hasVertical);
            var viewportHeight = this.GetViewportHeight(hasHorizontal);

            using (var textBrush = new SolidBrush(this.ForeColor))
            using (var trackBrush = new SolidBrush(this.ScrollbarTrackColor))
            using (var thumbBrush = new SolidBrush(this.ScrollbarThumbColor))
            using (var borderPen = new Pen(this.BorderColor))
            {
                var firstLine = this.lineHeight <= 0 ? 0 : this.verticalOffset / this.lineHeight;
                var y = -(this.verticalOffset % Math.Max(1, this.lineHeight));
                for (int index = firstLine; index < this.lines.Length && y < viewportHeight; index++)
                {
                    var lineBounds = new Rectangle(6 - this.horizontalOffset, y, Math.Max(0, viewportWidth + this.horizontalOffset - 8), this.lineHeight);
                    TextRenderer.DrawText(
                        graphics,
                        this.lines[index],
                        this.Font,
                        lineBounds,
                        this.ForeColor,
                        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                    y += this.lineHeight;
                }

                if (hasVertical)
                {
                    var trackBounds = new Rectangle(this.ClientSize.Width - ScrollbarThickness, 0, ScrollbarThickness, viewportHeight);
                    graphics.FillRectangle(trackBrush, trackBounds);
                    graphics.FillRectangle(thumbBrush, verticalThumb);
                    graphics.DrawRectangle(borderPen, trackBounds.X, trackBounds.Y, trackBounds.Width - 1, trackBounds.Height - 1);
                    graphics.DrawRectangle(borderPen, verticalThumb.X, verticalThumb.Y, verticalThumb.Width - 1, verticalThumb.Height - 1);
                }

                if (hasHorizontal)
                {
                    var trackBounds = new Rectangle(0, this.ClientSize.Height - ScrollbarThickness, viewportWidth, ScrollbarThickness);
                    graphics.FillRectangle(trackBrush, trackBounds);
                    graphics.FillRectangle(thumbBrush, horizontalThumb);
                    graphics.DrawRectangle(borderPen, trackBounds.X, trackBounds.Y, trackBounds.Width - 1, trackBounds.Height - 1);
                    graphics.DrawRectangle(borderPen, horizontalThumb.X, horizontalThumb.Y, horizontalThumb.Width - 1, horizontalThumb.Height - 1);
                }

                if (hasVertical && hasHorizontal)
                {
                    var cornerBounds = new Rectangle(this.ClientSize.Width - ScrollbarThickness, this.ClientSize.Height - ScrollbarThickness, ScrollbarThickness, ScrollbarThickness);
                    graphics.FillRectangle(trackBrush, cornerBounds);
                    graphics.DrawRectangle(borderPen, cornerBounds.X, cornerBounds.Y, cornerBounds.Width - 1, cornerBounds.Height - 1);
                }
            }
        }

        private void RecalculateLayout()
        {
            this.lines = (this.textContent ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (this.lines.Length == 0)
            {
                this.lines = new[] { string.Empty };
            }

            this.lineHeight = TextRenderer.MeasureText("Ag", this.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Height + 2;
            this.contentWidth = this.lines.Length == 0
                ? 0
                : this.lines.Max(line => TextRenderer.MeasureText(line, this.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width) + 12;
        }

        private bool IsAtBottom()
        {
            return this.verticalOffset >= this.GetMaxVerticalOffset();
        }

        private void ScrollToBottom()
        {
            this.verticalOffset = this.GetMaxVerticalOffset();
            this.Invalidate();
        }

        private void ClampOffsets()
        {
            this.verticalOffset = Math.Max(0, Math.Min(this.verticalOffset, this.GetMaxVerticalOffset()));
            this.horizontalOffset = Math.Max(0, Math.Min(this.horizontalOffset, this.GetMaxHorizontalOffset()));
        }

        private int GetContentHeight()
        {
            return this.lines.Length * Math.Max(1, this.lineHeight);
        }

        private int GetViewportWidth()
        {
            return this.GetViewportWidth(this.GetContentHeight() > this.GetViewportHeight(false));
        }

        private int GetViewportWidth(bool reserveVerticalScrollbar)
        {
            return Math.Max(0, this.ClientSize.Width - (reserveVerticalScrollbar ? ScrollbarThickness : 0));
        }

        private int GetViewportHeight()
        {
            return this.GetViewportHeight(this.contentWidth > this.GetViewportWidth(false));
        }

        private int GetViewportHeight(bool reserveHorizontalScrollbar)
        {
            return Math.Max(0, this.ClientSize.Height - (reserveHorizontalScrollbar ? ScrollbarThickness : 0));
        }

        private int GetMaxVerticalOffset()
        {
            return Math.Max(0, this.GetContentHeight() - this.GetViewportHeight());
        }

        private int GetMaxHorizontalOffset()
        {
            return Math.Max(0, this.contentWidth - this.GetViewportWidth());
        }

        private bool TryGetVerticalThumbBounds(out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            var viewportHeight = this.GetViewportHeight(false);
            var hasHorizontal = this.contentWidth > this.GetViewportWidth(false);
            if (hasHorizontal)
            {
                viewportHeight = this.GetViewportHeight(true);
            }

            var contentHeight = this.GetContentHeight();
            if (contentHeight <= viewportHeight || viewportHeight <= 0)
            {
                return false;
            }

            var thumbHeight = Math.Max(36, (int)Math.Round((viewportHeight / (double)contentHeight) * viewportHeight));
            var travel = Math.Max(1, viewportHeight - thumbHeight);
            var ratio = this.GetMaxVerticalOffset() == 0 ? 0D : this.verticalOffset / (double)this.GetMaxVerticalOffset();
            var y = (int)Math.Round(ratio * travel);
            bounds = new Rectangle(this.ClientSize.Width - ScrollbarThickness + 2, y + 2, ScrollbarThickness - 4, thumbHeight - 4);
            return true;
        }

        private bool TryGetHorizontalThumbBounds(out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            var viewportWidth = this.GetViewportWidth(false);
            var hasVertical = this.GetContentHeight() > this.GetViewportHeight(false);
            if (hasVertical)
            {
                viewportWidth = this.GetViewportWidth(true);
            }

            if (this.contentWidth <= viewportWidth || viewportWidth <= 0)
            {
                return false;
            }

            var thumbWidth = Math.Max(36, (int)Math.Round((viewportWidth / (double)this.contentWidth) * viewportWidth));
            var travel = Math.Max(1, viewportWidth - thumbWidth);
            var ratio = this.GetMaxHorizontalOffset() == 0 ? 0D : this.horizontalOffset / (double)this.GetMaxHorizontalOffset();
            var x = (int)Math.Round(ratio * travel);
            bounds = new Rectangle(x + 2, this.ClientSize.Height - ScrollbarThickness + 2, thumbWidth - 4, ScrollbarThickness - 4);
            return true;
        }

        private void UpdateVerticalDrag(int mouseY)
        {
            Rectangle thumbBounds;
            if (!this.TryGetVerticalThumbBounds(out thumbBounds))
            {
                return;
            }

            var viewportHeight = this.GetViewportHeight(this.contentWidth > this.GetViewportWidth(false));
            var thumbHeight = thumbBounds.Height + 4;
            var travel = Math.Max(1, viewportHeight - thumbHeight);
            var top = Math.Max(0, Math.Min(travel, mouseY - this.verticalDragOffset - 2));
            var ratio = top / (double)travel;
            this.verticalOffset = (int)Math.Round(ratio * this.GetMaxVerticalOffset());
            this.ClampOffsets();
            this.Invalidate();
        }

        private void UpdateHorizontalDrag(int mouseX)
        {
            Rectangle thumbBounds;
            if (!this.TryGetHorizontalThumbBounds(out thumbBounds))
            {
                return;
            }

            var viewportWidth = this.GetViewportWidth(this.GetContentHeight() > this.GetViewportHeight(false));
            var thumbWidth = thumbBounds.Width + 4;
            var travel = Math.Max(1, viewportWidth - thumbWidth);
            var left = Math.Max(0, Math.Min(travel, mouseX - this.horizontalDragOffset - 2));
            var ratio = left / (double)travel;
            this.horizontalOffset = (int)Math.Round(ratio * this.GetMaxHorizontalOffset());
            this.ClampOffsets();
            this.Invalidate();
        }
    }
}
