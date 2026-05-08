using System;
using System.Text.Json.Serialization;

namespace WinStasis.Models
{
    /// <summary>
    /// Represents the entire snapshot of the workspace at a specific point in time.
    /// </summary>
    public class SessionProfile
    {
        public string ProfileName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public WindowRecord[] Windows { get; set; } = Array.Empty<WindowRecord>();
    }

    /// <summary>
    /// Represents a single window captured in the snapshot.
    /// </summary>
    public class WindowRecord
    {
        // 1. IDENTIFIERS
        public int TargetId { get; set; }               // Stable ID for CLI targeting (e.g., --target 1)
        public long Hwnd { get; set; }                  // Volatile OS handle (saved as long because IntPtr isn't easily JSON serializable)
        public string ProcessName { get; set; } = "";   // Used for Fallback Hybrid Matching
        public string WindowTitle { get; set; } = "";   // Used for Fallback Hybrid Matching

        // 2. GEOMETRY
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int ShowCmd { get; set; }                // 1=Normal, 2=Minimized, 3=Maximized

        // 3. WORKSPACE
        public Guid DesktopId { get; set; }             // The unique GUID of the Virtual Desktop this window was on
    }
}
