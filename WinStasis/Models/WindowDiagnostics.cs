using System;

namespace WinStasis.Models
{
    public class WindowDiagnostics
    {
        public string ClassName { get; set; } = string.Empty;
        public long Style { get; set; }
        public long ExStyle { get; set; }
        public WindowRect RawWin32Rect { get; set; }
        public WindowRect VisibleDwmRect { get; set; }
        public WindowRect NormalPositionRect { get; set; }
        public WindowFrameMargins FrameMargins { get; set; }
    }
}
