# Offshoot Plan: Verbose Window Inspector CLI

**Status:** Proposed Offshoot Project

## 1. The Vision
A lightweight, dependency-free, read-only command-line utility that scans, maps, and logs every visible window on a Windows system. 

Instead of trying to fight the Windows operating system by forcibly moving windows across virtual desktops, this tool fully embraces the "Unix Philosophy": **Do one thing, and do it well.** It acts as a highly detailed "ls" command specifically for the Windows desktop UI layer, filling a modern gap that classic tools (like Spy++ or WinSpy) miss.

## 2. Why this is perfect for "Tiny Tool Town"
*   **Fills a Modern Gap:** Legacy inspection tools do not understand Windows 10/11 Virtual Desktops. Existing modern solutions (like Slion's VirtualDesktop or Python's `pyvda`) are developer libraries, not ready-to-use standalone CLI tools.
*   **Zero Dependencies:** Because it only *reads* data, it relies entirely on official, stable Microsoft Win32 and COM APIs. No Python environments or Nuget packages required.
*   **Highly Reliable:** It will work exactly the same on Windows 10 and Windows 11 without breaking during OS updates.

## 3. Core Requirements & Features (MVP)
The tool will execute a scan and output a verbose payload for every visible window.

**Data Points Captured:**
*   **Identity:** Window Handle (`HWND`), Process Name (`.exe`), Window Title.
*   **Geometry:** X/Y Coordinates, Width/Height.
*   **Virtual Workspace ID Mapping:** Extract the raw Virtual Desktop GUID and **translate it into human-readable Desktop Numbers (e.g., Workspace 1, Workspace 2)**. This is achieved by parsing the Windows Registry (`HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops`).

## 4. Future Features (Post-MVP)
*   **Workspace Names:** Extract user-customized desktop names (e.g., "Coding", "Browsing") from the registry.
*   **Mirrored/Pinned Windows:** Detect if a window has been set to "Show on all desktops". 
*   **Exporting:** Add a `--json` flag to dump the payload for consumption by other scripts.

## 5. Name Brainstorming
Focus: Virtual Desktops, App Listing, Mapping.

**Current Favorite:** 
*   `deskscout` (Scouting out the virtual desktops)

**Unix-Style "Lister" Names:**
*   `deskls` / `desk-ls` (Listing desktops)
*   `spacels` / `space-ls` (Listing workspaces)
*   `vdtree` (Like the unix `tree` command, but printing a tree of Virtual Desktops -> Windows)
*   `vdlist` (Virtual Desktop List)

**Descriptive & Mapping Names:**
*   `deskmap` (Mapping out the desktop topology)
*   `workspacemap`
*   `winatlas` (An atlas of your windows and workspaces)
*   `desktally` (Taking a tally of what is on each desktop)
