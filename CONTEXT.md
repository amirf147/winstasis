# Domain Glossary: winstasis

* **Win32 Native Boundary:** The unmanaged C++ layer of the Windows OS (`user32.dll`). 
* **Window State:** The geometric and visual properties of an application on screen (X/Y, Width/Height, ShowCmd).
* **Opaque Window Rule:** WinStasis manages the OS-level container. It does NOT inspect internal application state.
* **Window Handle (HWND):** The temporary, OS-assigned unique ID for a window.
* **Process ID (PID):** The temporary, OS-assigned unique ID for the running application.
* **Intra-Session Drift:** When windows are moved or workspaces are closed *without* restarting the applications. HWNDs remain valid during this drift.
* **Hybrid Matching:** The two-step process of finding a window during restore (1. HWND, 2. Process + Title).
* **Ambiguous Match:** When two windows share the exact same Process Name and Title. Resolved via First-Come, First-Served.
* **Execution Model (On-Demand):** `winstasis` is a manual, point-in-time script.
* **Display Topology Safety:** The tool aborts if the current monitor resolution does not match the saved layout's resolution.
* **Profile-Driven Storage:** Layouts are saved as explicit, user-named JSON profiles within a local `sessions/` directory (e.g., `winstasis --save coding`).
