# Research Spike: UI Automation Framework vs. Win32 API

**Status:** Research for Future Offshoots

## 1. Does our current method (`EnumWindows`) capture ALL window types?
**Yes, it captures the top-level container for virtually everything.** 

Windows uses a display system called the Desktop Window Manager (DWM). No matter what modern UI framework a developer uses to build their app—whether it’s Electron (Discord, VS Code), Chromium (Waterfox, Chrome), WPF, WinForms, WinUI3, or UWP (modern Windows 11 apps like Settings)—the operating system ultimately wraps it in a top-level `HWND` (Window Handle) so it can be moved around the screen.

When we use `EnumWindows` from `user32.dll`, we are asking the OS for a list of these top-level containers. 

## 2. What is Microsoft UI Automation (UIA)?
Microsoft UI Automation is a highly advanced accessibility framework. It was designed primarily for screen readers (like NVDA or Narrator) and automated software testing. 

Instead of just looking at the "Window", UIA builds a massive mathematical tree of **every single element on your screen**. If you open VS Code, UIA maps the window, but also every button, every line of text, every scrollbar, and every menu item.

## 3. UIA vs. Win32 for the `vdtree` / `deskscout` Offshoot
For the Virtual Desktop lister, **we should stick to the Win32 API.**
*   **Performance:** UIA is extremely slow compared to Win32 because it maps thousands of buttons across your whole screen. `EnumWindows` executes in ~2 milliseconds.
*   **No Virtual Desktop Support:** UIA is focused on *content*, so it does not natively understand Virtual Desktops or GUIDs. 
*   **Conclusion:** Win32 provides exactly what the lister needs instantly and with zero dependencies.

## 4. NEW OFFSHOOT: Sub-Window Contextual Grammars (Caster Integration)
While UIA is wrong for the Virtual Desktop mapping tool, **it is the perfect framework for a completely different accessibility project.**

**The Problem:** Currently, speech accessibility tools like Caster trigger grammars based on the *top-level window title* (e.g., "Anti-Gravity"). However, power users need fine-grained grammars that activate only when focus is inside a *specific pane* of an application (e.g., terminal commands should only activate when keyboard focus is inside the Editor's embedded terminal pane, not the main code editor).

**The UIA Solution:**
UIA has an event-driven mechanism called `AutomationFocusChangedEventHandler`. A lightweight C# background service could use UIA to monitor exactly which UI element currently holds the user's keyboard focus.
*   When focus shifts to a pane with `ClassName="TerminalControl"`, the UIA service instantly detects it.
*   The service can then broadcast a message (via IPC, WebSockets, or stdout) to Caster.
*   Caster dynamically loads the "Terminal Grammar" in real-time.

**Conclusion:** This is a highly viable, standalone micro-project. It leverages the exact strengths of UIA (deep structural introspection and focus tracking) to solve a major limitation in voice-driven programming.
