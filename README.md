# 🪟 winstasis

> **⚠️ DEVELOPMENT PAUSED**  
> Active development on `winstasis` has been suspended. While the tool successfully extracts and restores accurate multi-monitor window coordinates, the core goal of automated cross-workspace movement is blocked by deep-seated Windows OS security restrictions ("Access Denied" when attempting to move un-owned processes).  
> 
> Rather than relying on fragile, undocumented APIs that break during Windows updates, the robust Win32/COM extraction engine built here is being spun off into two distinct, stable projects:
> 
> **🌱 Current Offshoots Being Explored:**
> 1. **[vdtree / deskscout](docs/offshoot_window_inspector_plan.md):** A lightweight, read-only CLI tool that maps and lists all open windows across Virtual Desktops in a human-readable tree format.
> 2. **[Caster UIA Context Engine](docs/research_ui_automation_vs_win32.md):** A micro-service utilizing Microsoft UI Automation (UIA) to track keyboard focus inside specific application panes (e.g., an embedded terminal vs. a code editor) to trigger ultra fine-grained, context-aware speech grammars for the Caster accessibility toolkit.

---

**A lightweight, native C# Window Session Manager built for power users.**

*winstasis* is a command-line utility designed to capture, save, and seamlessly restore the exact positions, sizes, and states of Windows applications. It is built to tame the chaos of multi-workspace reboots and ensure that critical tools—including accessibility overlays and development environments—stay exactly where they belong.

## 🎯 Project Goals
This project serves a dual purpose:
1. **Utility:** Creating a "set-it-and-forget-it" tool to eliminate "window drift" and manual repositioning after system restarts.
2. **Pedagogy:** A deep-dive into C# and the Win32 API. This project is a stepping stone toward mastering Windows internals, with the long-term goal of developing advanced, low-level accessibility tools and high-performance window management solutions.

## 🛑 The "Access Denied" Barrier
During the development of Phase 3, we discovered that the official `IVirtualDesktopManager::MoveWindowToDesktop` API returns an **Access Denied (0x80070005)** error if a process attempts to move a window it does not explicitly own. This is a security design choice by Microsoft.

### 🔍 Research Findings
We documented a path forward, but it requires a significant architectural pivot that compromises the stability of the tool:
1. **Undocumented COM Interfaces**: To move windows globally, we must use `IVirtualDesktopManagerInternal`. This interface is undocumented and changes its memory layout (`vtable`) with almost every major Windows update.
2. **The Maintenance Trap**: Relying on undocumented APIs creates a "fragility debt." If the memory offsets shift, the application will crash instantly.
3. **Third-Party Solutions**: Open-source libraries like [Slion/VirtualDesktop](https://github.com/Slion/VirtualDesktop) manage this volatility by dynamically mapping offsets based on Windows build numbers.

For more technical details, see:
- [Research: Virtual Desktop Libraries](docs/research_virtual_desktop_libraries.md)
- [Research: Windhawk, C++, and Memory Fragility](docs/research_windhawk_and_cpp.md)

## 🛠️ Core Accomplishments Before Pause
- **Hybrid Matching:** Reliable window resolution using HWND and Process/Title fallbacks.
- **Boundary Clamping:** Safe restoration across changing monitor topologies (e.g., docking/undocking).
- **JSON Orchestration:** Fully functional session capture and CLI routing.
- **Contextual Overrides:** Intelligent state management (e.g., waking minimized windows).

## 🧠 Development Methodology
This project was engineered using an **Agentic Workflow**. It leveraged highly iterative development cycles, AI-assisted pair programming within the Anti-Gravity editor, and a "Grill-with-Docs" strategy to ensure the codebase always matches its architectural decisions (ADRs).

## 🛠️ Usage
Ensure you have the .NET SDK installed.

### Commands
```bash
# Save current layout across ALL Virtual Desktops
dotnet run -- save coding

# Overwrite an existing profile
dotnet run -- save coding --force

# List all windows in a profile with Target IDs and Workspace GUIDs
dotnet run -- list coding

# Restore all windows in a profile (Active Desktop only, due to OS constraints)
dotnet run -- restore coding

# Restore a specific window by its Target ID
dotnet run -- restore coding --target 5
```