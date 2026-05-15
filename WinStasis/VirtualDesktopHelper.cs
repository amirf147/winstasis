using System;
using System.Linq;
using WindowsDesktop;

namespace WinStasis
{
    /// <summary>
    /// Wrapper for the Windows 10/11 Virtual Desktop APIs using the Slions.VirtualDesktop library.
    /// This bypasses the OS "Access Denied" restriction by utilizing undocumented COM interfaces safely.
    /// </summary>
    internal class VirtualDesktopHelper
    {
        public VirtualDesktopHelper()
        {
            try 
            {
                // Most VirtualDesktop libraries require explicit initialization
                VirtualDesktop.Configure();
            } 
            catch (Exception ex)
            {
                Console.WriteLine($"[Debug] VirtualDesktop.Configure() failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves the GUID of the Virtual Desktop that the window belongs to.
        /// Returns Guid.Empty if the window is not assigned to a specific desktop or an error occurs.
        /// </summary>
        public Guid GetWindowDesktopId(IntPtr hWnd)
        {
            try
            {
                var desktop = VirtualDesktop.FromHwnd(hWnd);
                return desktop?.Id ?? Guid.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Debug] GetWindowDesktopId failed for {hWnd}: {ex.Message}");
                return Guid.Empty;
            }
        }

        /// <summary>
        /// Moves a window to the specified Virtual Desktop GUID using Slions.VirtualDesktop.
        /// </summary>
        public bool MoveWindowToDesktop(IntPtr hWnd, Guid targetDesktopId)
        {
            if (targetDesktopId == Guid.Empty)
                return false;

            try
            {
                // Find the desktop object matching the GUID
                var targetDesktop = VirtualDesktop.GetDesktops().FirstOrDefault(d => d.Id == targetDesktopId);
                
                if (targetDesktop == null)
                {
                    Console.WriteLine($"            -> [Debug] Target desktop GUID {targetDesktopId} no longer exists.");
                    return false;
                }

                // If it's already on the correct desktop, skip moving
                var currentDesktop = VirtualDesktop.FromHwnd(hWnd);
                if (currentDesktop?.Id == targetDesktopId)
                {
                    return true;
                }

                // Execute the move (The Holy Grail)
                VirtualDesktop.MoveToDesktop(hWnd, targetDesktop);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"            -> [Debug] Exception moving workspace: {ex.Message}");
                return false;
            }
        }
    }
}
