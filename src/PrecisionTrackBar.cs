using System;
using System.Windows.Forms;

namespace NavimowDesktopController
{
    internal sealed class PrecisionTrackBar : TrackBar
    {
        private const int WmMouseWheel = 0x020A;

        public int MouseWheelStep { get; set; }

        public PrecisionTrackBar()
        {
            this.MouseWheelStep = 1;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmMouseWheel)
            {
                var rawDelta = (short)(((long)m.WParam >> 16) & 0xFFFF);
                if (rawDelta != 0)
                {
                    var step = Math.Max(1, this.MouseWheelStep);
                    var nextValue = Math.Max(this.Minimum, Math.Min(this.Maximum, this.Value + (Math.Sign(rawDelta) * step)));
                    if (nextValue != this.Value)
                    {
                        this.Value = nextValue;
                        this.OnScroll(EventArgs.Empty);
                    }
                }

                return;
            }

            base.WndProc(ref m);
        }
    }
}
