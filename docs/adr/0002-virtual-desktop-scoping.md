# 2. Virtual Desktop Scoping Strategy

**Status:** Accepted

## Context
`winstasis` needs to handle window layouts when a user utilizes Windows 10/11 Virtual Desktops (Workspaces). A user expects to be able to save and restore windows on a per-workspace basis.

However, the official Windows API (`IVirtualDesktopManager`) is highly restricted. It exposes methods to check if a window is on the *current* desktop (`IsWindowOnCurrentVirtualDesktop`) and to move a window, but it does *not* officially expose a way to enumerate all existing Virtual Desktops or fetch windows from inactive desktops without resorting to fragile, undocumented registry hacks or COM interfaces that break during OS updates.

## Decision
We will adopt the **Active Workspace (Option A)** approach. `winstasis` will strictly operate *only* on the Virtual Desktop that is currently active when the command is run. 
* Saving a snapshot will filter out any windows where `IsWindowOnCurrentVirtualDesktop` is false.
* Restoring a snapshot will only attempt to find and manipulate windows on the currently active desktop.

## Consequences
* **Positive:** The codebase remains clean, officially supported, and highly stable across Windows updates.
* **Positive:** Conceptually simpler for the user ("It manages what I'm looking at right now").
* **Negative:** It is impossible to run `winstasis list` and see windows from a different, inactive virtual desktop. The user must manually switch to the workspace first.
