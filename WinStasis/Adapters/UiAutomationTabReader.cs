using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace WinStasis.Adapters
{
    /// <summary>
    /// Shallow native COM reader for UI Automation TabItem elements.
    /// Marshals tab names in a single bulk cross-process query.
    /// </summary>
    public static class UiAutomationTabReader
    {
        private const int TreeScope_Descendants = 4;
        private const int UIA_ControlTypePropertyId = 30003;
        private const int UIA_TabItemControlTypeId = 50019;
        private const int UIA_NamePropertyId = 30005;
        private const int MaxTabsPerWindow = 64;

        private const uint WM_GETOBJECT = 0x003D;
        private const int OBJID_CLIENT = -4;
        private const uint SMTO_ABORTIFHUNG = 0x0002;

        private static readonly HashSet<string> ExcludedTabNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Problems",
            "Output",
            "Debug Console",
            "Terminal",
            "Ports",
            "Comments"
        };

        public static string[] ReadWindowTabs(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                return Array.Empty<string>();
            }

            try
            {
                var sb = new StringBuilder(256);
                GetClassName(hWnd, sb, sb.Capacity);
                string className = sb.ToString();
                bool isMozilla = className.Equals("MozillaWindowClass", StringComparison.OrdinalIgnoreCase);

                var uia = (IUIAutomation)new CUIAutomation();
                uia.ElementFromHandle(hWnd, out var rootElement);
                if (rootElement == null)
                {
                    return Array.Empty<string>();
                }

                uia.CreatePropertyCondition(UIA_ControlTypePropertyId, UIA_TabItemControlTypeId, out var tabCondition);
                if (tabCondition == null)
                {
                    return Array.Empty<string>();
                }

                uia.CreateCacheRequest(out var cacheRequest);
                if (cacheRequest == null)
                {
                    return Array.Empty<string>();
                }

                cacheRequest.AddProperty(UIA_NamePropertyId);

                // Use RawViewCondition to traverse cloaked/background desktop subtrees (DWM_CLOAKED_SHELL)
                uia.get_RawViewCondition(out var rawView);
                if (rawView != null)
                {
                    cacheRequest.put_TreeFilter(rawView);
                }

                rootElement.FindAllBuildCache(TreeScope_Descendants, tabCondition, cacheRequest, out var tabElements);

                // Gecko lazy accessibility initialization: if MozillaWindowClass returned 0 elements,
                // wake the accessible root via WM_GETOBJECT and retry once after a short warmup.
                if (isMozilla && (tabElements == null || tabElements.Length == 0))
                {
                    SendMessageTimeout(hWnd, WM_GETOBJECT, IntPtr.Zero, (IntPtr)OBJID_CLIENT, SMTO_ABORTIFHUNG, 100, out _);
                    Thread.Sleep(50);
                    uia.ElementFromHandle(hWnd, out var refreshedRoot);
                    var searchRoot = refreshedRoot ?? rootElement;
                    searchRoot.FindAllBuildCache(TreeScope_Descendants, tabCondition, cacheRequest, out tabElements);
                }

                if (tabElements == null)
                {
                    return Array.Empty<string>();
                }

                int count = tabElements.Length;
                if (count <= 0)
                {
                    return Array.Empty<string>();
                }

                int limit = Math.Min(count, MaxTabsPerWindow);
                var tabs = new List<string>(limit);

                for (int i = 0; i < count && tabs.Count < MaxTabsPerWindow; i++)
                {
                    tabElements.GetElement(i, out var tabElement);
                    if (tabElement == null)
                    {
                        continue;
                    }

                    tabElement.GetCachedPropertyValue(UIA_NamePropertyId, out object? nameObj);
                    if (nameObj is string name)
                    {
                        string trimmed = name.Trim();
                        if (!string.IsNullOrEmpty(trimmed) && !ExcludedTabNames.Contains(trimmed))
                        {
                            tabs.Add(trimmed);
                        }
                    }
                }

                return tabs.ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint Msg,
            IntPtr wParam,
            IntPtr lParam,
            uint fuFlags,
            uint uTimeout,
            out IntPtr lpdwResult);

        // =====================================================================
        // NATIVE COM DEFINITIONS (UIAutomationCore.dll)
        // =====================================================================

        [ComImport]
        [Guid("ff48dba4-60ef-4201-aa87-54103eef594e")]
        [ClassInterface(ClassInterfaceType.None)]
        private class CUIAutomation
        {
        }

        [ComImport]
        [Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IUIAutomation
        {
            // 0: CompareElements
            void CompareElements(IntPtr el1, IntPtr el2, [MarshalAs(UnmanagedType.Bool)] out bool areSame);
            // 1: CompareRuntimeIds
            void CompareRuntimeIds(IntPtr runtimeId1, IntPtr runtimeId2, [MarshalAs(UnmanagedType.Bool)] out bool areSame);
            // 2: GetRootElement
            void GetRootElement(out IUIAutomationElement root);
            // 3: ElementFromHandle
            void ElementFromHandle(IntPtr hwnd, out IUIAutomationElement element);
            // 4: ElementFromPoint
            void ElementFromPoint(NativePoint pt, out IUIAutomationElement element);
            // 5: GetFocusedElement
            void GetFocusedElement(out IUIAutomationElement element);
            // 6: GetRootElementBuildCache
            void GetRootElementBuildCache(IUIAutomationCacheRequest cacheRequest, out IUIAutomationElement root);
            // 7: ElementFromHandleBuildCache
            void ElementFromHandleBuildCache(IntPtr hwnd, IUIAutomationCacheRequest cacheRequest, out IUIAutomationElement element);
            // 8: ElementFromPointBuildCache
            void ElementFromPointBuildCache(NativePoint pt, IUIAutomationCacheRequest cacheRequest, out IUIAutomationElement element);
            // 9: GetFocusedElementBuildCache
            void GetFocusedElementBuildCache(IUIAutomationCacheRequest cacheRequest, out IUIAutomationElement element);
            // 10: CreateTreeWalker
            void CreateTreeWalker(IUIAutomationCondition pCondition, out IntPtr walker);
            // 11: get_ControlViewWalker
            void get_ControlViewWalker(out IntPtr walker);
            // 12: get_ContentViewWalker
            void get_ContentViewWalker(out IntPtr walker);
            // 13: get_RawViewWalker
            void get_RawViewWalker(out IntPtr walker);
            // 14: get_RawViewCondition
            void get_RawViewCondition(out IUIAutomationCondition condition);
            // 15: get_ControlViewCondition
            void get_ControlViewCondition(out IUIAutomationCondition condition);
            // 16: get_ContentViewCondition
            void get_ContentViewCondition(out IUIAutomationCondition condition);
            // 17: CreateCacheRequest
            void CreateCacheRequest(out IUIAutomationCacheRequest cacheRequest);
            // 18: CreateTrueCondition
            void CreateTrueCondition(out IUIAutomationCondition newCondition);
            // 19: CreateFalseCondition
            void CreateFalseCondition(out IUIAutomationCondition newCondition);
            // 20: CreatePropertyCondition
            void CreatePropertyCondition(int propertyId, [MarshalAs(UnmanagedType.Struct)] object value, out IUIAutomationCondition newCondition);
        }

        [ComImport]
        [Guid("352ffba8-0973-437c-a61f-f64cafd81df9")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IUIAutomationCondition
        {
        }

        [ComImport]
        [Guid("b32a92b5-bc25-4078-9c08-d7ee95c48e03")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IUIAutomationCacheRequest
        {
            void AddProperty(int propertyId);
            void AddPattern(int patternId);
            void Clone(out IUIAutomationCacheRequest clonedRequest);
            void get_TreeScope(out int scope);
            void put_TreeScope(int scope);
            void get_TreeFilter(out IUIAutomationCondition filter);
            void put_TreeFilter(IUIAutomationCondition filter);
            void get_AutomationElementMode(out int mode);
            void put_AutomationElementMode(int mode);
        }

        [ComImport]
        [Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IUIAutomationElement
        {
            void SetFocus();
            void GetRuntimeId(out IntPtr runtimeId);
            void FindFirst(int scope, IUIAutomationCondition condition, out IUIAutomationElement found);
            void FindAll(int scope, IUIAutomationCondition condition, out IUIAutomationElementArray found);
            void FindFirstBuildCache(int scope, IUIAutomationCondition condition, IUIAutomationCacheRequest cacheRequest, out IUIAutomationElement found);
            void FindAllBuildCache(int scope, IUIAutomationCondition condition, IUIAutomationCacheRequest cacheRequest, out IUIAutomationElementArray found);
            void BuildUpdatedCache(IUIAutomationCacheRequest cacheRequest, out IUIAutomationElement updatedElement);
            void GetCurrentPropertyValue(int propertyId, [MarshalAs(UnmanagedType.Struct)] out object? retVal);
            void GetCurrentPropertyValueEx(int propertyId, [MarshalAs(UnmanagedType.Bool)] bool ignoreDefaultValue, [MarshalAs(UnmanagedType.Struct)] out object? retVal);
            void GetCachedPropertyValue(int propertyId, [MarshalAs(UnmanagedType.Struct)] out object? retVal);
        }

        [ComImport]
        [Guid("14314595-b4bc-4055-95f2-58f2e42c9855")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IUIAutomationElementArray
        {
            int Length { [return: MarshalAs(UnmanagedType.I4)] get; }
            void GetElement(int index, out IUIAutomationElement element);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }
    }
}

