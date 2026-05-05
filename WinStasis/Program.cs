using System;
using System.Diagnostics;
using System.Runtime.InteropServices; // Required for all P/Invoke (Platform Invoke) and unmanaged code interaction
using System.Text;

namespace WinStasis
{
    class Program
    {
        // =====================================================================
        // 1. THE DELEGATE & CALLBACK CONCEPT
        // =====================================================================
        // Windows API functions often don't return arrays. Instead, they use "Callbacks".
        // We define a Delegate here, which is essentially a type-safe function pointer.
        // We will hand this delegate to Windows, and Windows will execute our function
        // once for every single window it finds.
        // IntPtr (Integer Pointer) is how C# represents a C++ pointer or a Windows "Handle" (HWND).
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        // =====================================================================
        // 2. WIN32 API IMPORTS (P/Invoke)
        // =====================================================================
        // We use [DllImport] to tell the C# compiler: "Do not look for the body of this 
        // function in this project. Look inside the operating system's user32.dll file."
        // We use 'extern' to declare the signature without providing the implementation.

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        // MarshalAs translates the C++ 4-byte BOOL into the C# 1-byte bool so memory aligns correctly.
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        // CharSet.Auto tells the marshaller to automatically decide between ANSI or Unicode 
        // depending on the version of Windows. SetLastError allows us to catch Win32 error codes.
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        // 'out' keyword: We are telling C# that Windows will allocate the value for lpdwProcessId.
        // We just provide the empty bucket.
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        // 'ref' keyword: Unlike 'out', 'ref' means we must initialize the object in C# FIRST, 
        // pass it to Windows, and Windows will modify the existing object in memory.
        public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);


        // =====================================================================
        // 3. UNMANAGED DATA STRUCTURES (Structs)
        // =====================================================================
        // C# normally optimizes memory by moving variables around. We CANNOT allow that here.
        // [StructLayout(LayoutKind.Sequential)] forces C# to store these variables in the exact 
        // byte-for-byte physical order written below. If we don't do this, when Windows tries 
        // to write C++ data into our C# struct, it will write to the wrong memory addresses.

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;     // The size of this struct in bytes (required by Win32 API)
            public int flags;      // Internal state flags
            public int showCmd;    // 1 = Normal, 2 = Minimized, 3 = Maximized
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        // =====================================================================
        // 4. MAIN APPLICATION LOGIC
        // =====================================================================

        static void Main(string[] args)
        {
            Console.WriteLine("Scanning for visible windows...\n");
            Console.WriteLine(new string('-', 80));

            // We call the external OS function and pass it our C# method (FilterAndPrintWindow).
            // IntPtr.Zero is just a null pointer because we don't need to pass any extra parameters.
            EnumWindows(FilterAndPrintWindow, IntPtr.Zero);

            Console.WriteLine(new string('-', 80));
            Console.WriteLine("Scan complete.");
        }

        // This is the Callback function. Windows will execute this once for every window.
        // hWnd is the unique ID (Handle) for the current window being evaluated.
        private static bool FilterAndPrintWindow(IntPtr hWnd, IntPtr lParam)
        {
            // 1. FILTER VISIBILITY
            // The OS tracks hundreds of invisible windows used for background tasks.
            // If it's not visible to the user, we return 'true' to tell EnumWindows to skip to the next one.
            if (!IsWindowVisible(hWnd))
                return true;

            // 2. EXTRACT TITLE
            // C++ doesn't return strings easily. We have to create a pre-allocated memory buffer 
            // (StringBuilder) of 256 characters, and ask Windows to write text into that specific memory space.
            StringBuilder titleBuilder = new StringBuilder(256);
            GetWindowText(hWnd, titleBuilder, 256);
            string windowTitle = titleBuilder.ToString().Trim();

            // Ignore system overlays that have no title.
            if (string.IsNullOrEmpty(windowTitle))
                return true; 

            // 3. EXTRACT PROCESS NAME
            // Windows tracks windows by an internal Process ID (PID), not by the ".exe" name.
            GetWindowThreadProcessId(hWnd, out uint processId);
            string processName = "Unknown";
            try
            {
                // We ask the .NET framework to look up the human-readable name associated with the PID.
                Process proc = Process.GetProcessById((int)processId);
                processName = proc.ProcessName;
            }
            catch (Exception)
            {
                // Some high-security OS processes will deny our request. We catch the error to prevent crashing.
            }

            // Hardcoded exclusion of modern Windows 11 UI elements (Start menu, invisible taskbar wrappers).
            if (processName == "TextInputHost" || processName == "ApplicationFrameHost")
                return true;

            // 4. EXTRACT COORDINATES
            GetWindowRect(hWnd, out RECT rect);
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            // 5. EXTRACT STATE
            WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
            // Win32 API quirk: Before passing a struct using 'ref', we MUST tell the OS exactly how many 
            // bytes the struct takes up in memory, so it knows how much data it is allowed to write.
            placement.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
            GetWindowPlacement(hWnd, ref placement);
            
            // C# 8.0 Switch Expression to cleanly map OS integers to readable strings.
            string state = placement.showCmd switch
            {
                1 => "Normal",
                2 => "Minimized",
                3 => "Maximized",
                _ => "Unknown"
            };

            // 6. OUTPUT RESULTS
            Console.WriteLine($"Process : {processName}.exe");
            Console.WriteLine($"Title   : {windowTitle}");
            Console.WriteLine($"Position: X: {rect.Left}, Y: {rect.Top} | Size: {width}x{height}");
            Console.WriteLine($"State   : {state}");
            Console.WriteLine();

            // Returning true tells EnumWindows: "I am done with this window, give me the next one."
            return true; 
        }
    }
}