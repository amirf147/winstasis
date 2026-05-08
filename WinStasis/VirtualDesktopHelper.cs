using System;
using System.Runtime.InteropServices;

namespace WinStasis
{
    internal class VirtualDesktopHelper
    {
        // =====================================================================
        // 1. OFFICIAL INTERFACES (For Extraction)
        // =====================================================================
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
        private interface IVirtualDesktopManager
        {
            [PreserveSig] int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out bool onCurrentDesktop);
            [PreserveSig] int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);
            [PreserveSig] int MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId); // Refused by OS due to Access Denied
        }

        [ComImport]
        [Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")]
        private class VirtualDesktopManager { }

        // =====================================================================
        // 2. UNDOCUMENTED INTERFACES (For Movement)
        // =====================================================================
        // We must talk to the Immersive Shell to bypass the own-process restriction.
        [ComImport]
        [Guid("c2f03a33-21f5-47fa-b4bb-156362a2f239")]
        private class ImmersiveShell { }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("6d5140c1-7436-11ce-8034-00aa006009fa")]
        private interface IServiceProvider
        {
            [PreserveSig] int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject);
        }

        // Windows 11 Build 22621+ IVirtualDesktopManagerInternal GUID
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("a3175f2d-239c-4bd2-bea3-e07e6e026ce1")]
        private interface IVirtualDesktopManagerInternal
        {
            // The memory layout of this interface is extremely volatile. 
            // We pad it with dummy functions to hit the exact memory offset for MoveViewToDesktop.
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int MoveViewToDesktop(object pView, IntPtr desktop); // The holy grail
            // (There are about 20 other methods here, but we don't map them because we only need the pointer address).
        }

        // =====================================================================
        // 3. APPLICATIONVIEW INTERFACES
        // =====================================================================
        // To use the undocumented API, we can't just pass an HWND. We have to convert the HWND 
        // into a modern WinRT "ApplicationView" object first.
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
        [Guid("372e1d3b-38d3-42e4-a15b-8ab2b178f513")]
        private interface IApplicationView { }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("1841c6d7-4f9d-42c0-af41-8747538f10e5")]
        private interface IApplicationViewCollection
        {
            [PreserveSig] int GetViews(out IntPtr array);
            [PreserveSig] int GetViewsByZOrder(out IntPtr array);
            [PreserveSig] int GetViewsByAppUserModelId(string id, out IntPtr array);
            [PreserveSig] int GetViewForHwnd(IntPtr hwnd, out IApplicationView view); // Converts HWND to View
        }

        // Windows 11 Build 22621+ IVirtualDesktop interface (Represents the actual target desktop)
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("3F07F4BE-B107-441A-AF0C-4D718684D9CB")]
        private interface IVirtualDesktop
        {
            [PreserveSig] int IsViewVisible(IApplicationView view, out int visible);
            [PreserveSig] int GetID(out Guid id);
        }

        // =====================================================================
        // STATE & INITIALIZATION
        // =====================================================================
        private readonly IVirtualDesktopManager? _manager;
        private readonly IServiceProvider? _shellProvider;
        private readonly IApplicationViewCollection? _viewCollection;

        public VirtualDesktopHelper()
        {
            try
            {
                // Init Official Manager (for extraction)
                _manager = (IVirtualDesktopManager)new VirtualDesktopManager();

                // Init Undocumented Immersive Shell (for moving)
                _shellProvider = (IServiceProvider)new ImmersiveShell();

                // Ask the shell for the ApplicationViewCollection service
                Guid serviceGuid = typeof(IApplicationViewCollection).GUID;
                Guid interfaceGuid = typeof(IApplicationViewCollection).GUID;
                _shellProvider.QueryService(ref serviceGuid, ref interfaceGuid, out IntPtr viewCollectionPtr);
                
                if (viewCollectionPtr != IntPtr.Zero)
                {
                    _viewCollection = (IApplicationViewCollection)Marshal.GetObjectForIUnknown(viewCollectionPtr);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] Virtual Desktop APIs failed to initialize. Error: {ex.Message}");
            }
        }

        // =====================================================================
        // PUBLIC METHODS
        // =====================================================================
        public Guid GetWindowDesktopId(IntPtr hWnd)
        {
            if (_manager == null) return Guid.Empty;
            int hr = _manager.GetWindowDesktopId(hWnd, out Guid desktopId);
            return hr == 0 ? desktopId : Guid.Empty;
        }

        public bool MoveWindowToDesktop(IntPtr hWnd, Guid targetDesktopId)
        {
            if (_shellProvider == null || _viewCollection == null || targetDesktopId == Guid.Empty)
                return false;

            // Step 1: Check if it's already on the correct desktop using the official API.
            // This prevents us from firing complex undocumented COM calls unnecessarily.
            Guid currentDesktopId = GetWindowDesktopId(hWnd);
            if (currentDesktopId == targetDesktopId)
                return true; 

            try
            {
                // Step 2: Convert the raw HWND into a WinRT ApplicationView object
                int hr = _viewCollection.GetViewForHwnd(hWnd, out IApplicationView appView);
                if (hr != 0 || appView == null)
                {
                    Console.WriteLine($"            -> [Debug] Failed to convert HWND to AppView. HRESULT: 0x{hr:X}");
                    return false;
                }

                // Step 3: Request the undocumented IVirtualDesktopManagerInternal service from the Shell
                Guid serviceGuid = typeof(IVirtualDesktopManagerInternal).GUID;
                Guid interfaceGuid = typeof(IVirtualDesktopManagerInternal).GUID;
                _shellProvider.QueryService(ref serviceGuid, ref interfaceGuid, out IntPtr internalManagerPtr);
                
                if (internalManagerPtr == IntPtr.Zero)
                {
                    Console.WriteLine("            -> [Debug] Failed to get IVirtualDesktopManagerInternal pointer.");
                    return false;
                }

                // Step 4: We can't just pass the GUID to the move function. We have to search the OS 
                // for the actual IVirtualDesktop COM object that matches the target GUID.
                // NOTE: Implementing this requires mapping the `GetDesktops` array which is notoriously 
                // prone to crashing C#. 

                // For this iteration, we will leave the skeleton in place, but exit early 
                // to prevent memory corruption while we evaluate the exact Windows 11 Build number constraints.
                Console.WriteLine($"            -> [Debug] Reached undocumented COM boundary. Aborting move to prevent memory violation on Build 22621+.");
                
                Marshal.Release(internalManagerPtr);
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"            -> [Debug] Undocumented COM Exception: {ex.Message}");
                return false;
            }
        }
    }
}
