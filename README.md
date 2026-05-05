# 🪟 winstasis

**A lightweight, native C# Window Session Manager built for power users.**

*winstasis* is a command-line utility designed to capture, save, and seamlessly restore the exact positions, sizes, and states of Windows applications. It is built to tame the chaos of multi-workspace reboots and ensure that critical tools—including accessibility overlays and development environments—stay exactly where they belong.

## 🎯 Project Goals
This project serves a dual purpose:
1. **Utility:** Creating a "set-it-and-forget-it" tool to eliminate "window drift" and manual repositioning after system restarts.
2. **Pedagogy:** A deep-dive into C# and the Win32 API. This project is a stepping stone toward mastering Windows internals, with the long-term goal of developing advanced, low-level accessibility tools and high-performance window management solutions.

## 🚀 Current Status: Phase 1 (The Observer Spike)
Currently in the proof-of-concept phase. The application successfully hooks into the Win32 API (`User32.dll`) via P/Invoke to:
- Enumerate all top-level windows.
- Filter for visible, user-facing applications.
- Extract precise coordinates, dimensions, and window states (Maximized/Minimized/Normal).

## 🧠 Development Methodology
This project is engineered using an **Agentic Workflow**. It leverages highly iterative development cycles, AI-assisted pair programming within the Anti-Gravity editor, and strict, component-driven spikes to ensure high code quality and architectural clarity.

## 🛠️ Usage (Phase 1)
Ensure you have the .NET SDK installed.

```bash
cd WinStasis
dotnet run
```