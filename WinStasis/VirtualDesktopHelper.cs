using System;
using System.Runtime.InteropServices;

namespace WinStasis
{
    /// <summary>
    /// Wrapper for the official Windows 10/11 IVirtualDesktopManager COM interface.
    /// This allows us to extract the unique GUID of the workspace a window resides on.
    /// </summary>
    internal class VirtualDesktopHelper
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

            if (hr == 0)
            {
                return desktopId;
            }

            return Guid.Empty;
        }

        /// <summary>
        /// Moves a window to the specified Virtual Desktop GUID.
        /// Handles ADR-0006 Option B (Fallback to Current Workspace) implicitly by catching errors 
        /// (if the GUID doesn't exist, the COM call fails and we just leave the window where it is).
        /// </summary>
        public bool MoveWindowToDesktop(IntPtr hWnd, Guid desktopId)
        {
            if (_manager == null || desktopId == Guid.Empty)
                return false;

            try
            {
                int hr = _manager.MoveWindowToDesktop(hWnd, ref desktopId);

                // TEMPORARY DEBUG LOGGING to see why Windows is rejecting the move

                if (hr != 0)
                {
                    Console.WriteLine($"            -> [Debug] Windows API refused to move workspace. HRESULT: 0x{hr:X}");
                }


                return hr == 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"            -> [Debug] Exception moving workspace: {ex.Message}");
                return false;
            }
        }
    }
}
