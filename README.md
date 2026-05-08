# 🪟 winstasis

**A lightweight, native C# Window Session Manager built for power users.**

*winstasis* is a command-line utility designed to capture, save, and seamlessly restore the exact positions, sizes, and states of Windows applications. It is built to tame the chaos of multi-workspace reboots and ensure that critical tools—including accessibility overlays and development environments—stay exactly where they belong.

## 🎯 Project Goals
This project serves a dual purpose:
1. **Utility:** Creating a "set-it-and-forget-it" tool to eliminate "window drift" and manual repositioning after system restarts.
2. **Pedagogy:** A deep-dive into C# and the Win32 API. This project is a stepping stone toward mastering Windows internals, with the long-term goal of developing advanced, low-level accessibility tools and high-performance window management solutions.

## 🚀 Current Status: Phase 2 (The State Manager) - **CORE LOGIC COMPLETE**
We have successfully implemented the state management layer, enabling persistent window snapshots with full Virtual Desktop awareness.

**Accomplishments:**
- **CLI Routing:** Full support for `save`, `list`, and `restore` (Stub) commands.
- **Omniscient Workspace Tracking:** Real-time extraction of Virtual Desktop GUIDs using official `IVirtualDesktopManager` COM interfaces.
- **JSON Persistence:** Snapshots are serialized into the `sessions/` directory with detailed geometric and process metadata.
- **Safety & Sanitization:** Implemented profile overwrite protection and filename sanitization.

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

# Restore a layout (Phase 3: Development in progress)
# dotnet run -- restore coding
```
