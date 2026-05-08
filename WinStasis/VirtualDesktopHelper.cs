using System;
using System.Runtime.InteropServices;

namespace WinStasis
{
    /// <summary>
    /// Wrapper for the official Windows 10/11 IVirtualDesktopManager COM interface.
    /// This allows us to extract the unique GUID of the workspace a window resides on.
    /// </summary>
    public class VirtualDesktopHelper
    {
        // 1. COM INTERFACE DEFINITION
        // This is the C# translation of the unmanaged C++ IVirtualDesktopManager interface.
        // The GUID "a5cd92ff-29be-454c-8d04-d82879fb3f1b" is the official Microsoft ID for this interface.
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
        private interface IVirtualDesktopManager
        {
            [PreserveSig]
            int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out bool onCurrentDesktop);

            [PreserveSig]
            int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);

            [PreserveSig]
            int MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
        }

        // 2. COM OBJECT INSTANTIATION CLASS
        // This acts as the factory to create the actual manager object.
        [ComImport]
        [Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")]
        private class VirtualDesktopManager
        {
        }

        // 3. INTERNAL STATE
        private readonly IVirtualDesktopManager? _manager;

        public VirtualDesktopHelper()
        {
            try
            {
                // We attempt to instantiate the COM object. This will fail if running on Windows 7 or 8.
                _manager = (IVirtualDesktopManager)new VirtualDesktopManager();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] Failed to initialize VirtualDesktopManager. Workspace tracking will be disabled. Error: {ex.Message}");
                _manager = null;
            }
        }

        // 4. PUBLIC METHODS

        /// <summary>
        /// Retrieves the GUID of the Virtual Desktop that the window belongs to.
        /// Returns Guid.Empty if the window is not assigned to a specific desktop or an error occurs.
        /// </summary>
        public Guid GetWindowDesktopId(IntPtr hWnd)
        {
            if (_manager == null)
                return Guid.Empty;

            int hr = _manager.GetWindowDesktopId(hWnd, out Guid desktopId);

            // HRESULT 0 (S_OK) means success.
            // Other codes (like 0x8002802B) usually mean the window isn't tied to a specific desktop 
            // (e.g., it's a global overlay, or it's on a completely different physical monitor setup).

            if (hr == 0)
            {
                return desktopId;
            }

            return Guid.Empty;
        }
    }
}
