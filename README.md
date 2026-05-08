# 🪟 winstasis

**A lightweight, native C# Window Session Manager built for power users.**

*winstasis* is a command-line utility designed to capture, save, and seamlessly restore the exact positions, sizes, and states of Windows applications. It is built to tame the chaos of multi-workspace reboots and ensure that critical tools—including accessibility overlays and development environments—stay exactly where they belong.

## 🎯 Project Goals
This project serves a dual purpose:
1. **Utility:** Creating a "set-it-and-forget-it" tool to eliminate "window drift" and manual repositioning after system restarts.
2. **Pedagogy:** A deep-dive into C# and the Win32 API. This project is a stepping stone toward mastering Windows internals, with the long-term goal of developing advanced, low-level accessibility tools and high-performance window management solutions.

## 🚀 Current Status: Phase 3 (The Mover) - **IN PROGRESS (BLOCKED)**
Phase 3 is currently blocked by a security restriction in the Win32/COM layer. While window positioning is functional, workspace restoration is limited to the current process.

### 🛑 Blocking Error: Access Denied (0x80070005)
The official `IVirtualDesktopManager::MoveWindowToDesktop` API returns an Access Denied error when attempting to move windows owned by other processes (e.g., Chrome, VS Code, Spotify). This prevents full workspace restoration from a CLI context.

**Proposed Resolution:** Transition to the undocumented `IVirtualDesktopManagerInternal` interface (see [ADR-0007](docs/adr/0007-undocumented-com-interface.md)).

**Accomplishments:**
- **Hybrid Matching:** Reliable window resolution using HWND and Process/Title fallbacks.
- **Boundary Clamping:** Safe restoration across changing monitor topologies (e.g., docking/undocking).
- **Cross-Desktop Movement:** Initial COM integration (Official API).
- **Contextual Overrides:** Intelligent state management (e.g., waking minimized windows when targeted specifically).

## 🧠 Development Methodology
This project is engineered using an **Agentic Workflow**. It leverages highly iterative development cycles, AI-assisted pair programming within the Anti-Gravity editor, and a "Grill-with-Docs" strategy to ensure the codebase always matches its architectural decisions (ADRs).

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

# Restore all windows in a profile
dotnet run -- restore coding

# Restore a specific window by its Target ID
dotnet run -- restore coding --target 5
```