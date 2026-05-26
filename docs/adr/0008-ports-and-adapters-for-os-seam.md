# 8. Ports and Adapters for OS Seam

Date: 2026-05-26

## Status

Accepted

## Context

The `winst` application is tightly coupled to the Windows Operating System through direct use of Win32 APIs (via P/Invoke in `user32.dll`) and undocumented COM interfaces (via `Slions.VirtualDesktop`). 

Previously, the CLI module (`Program.cs`) and the domain logic (`WindowRestorer.cs`) directly manipulated native structs like `RECT` and `WINDOWPLACEMENT` and invoked native methods. This shallow coupling caused the domain logic (such as Hybrid Matching and Boundary Clamping) to become entangled with OS invocation rules, making it impossible to unit test the restoration state machine without a live Windows desktop environment.

## Decision

We will follow the **Ports and Adapters** pattern for all OS and Window Management interactions. 

1. **The Port:** We define `IWindowingEnvironment`, an interface describing the low-level primitives required by the domain logic (e.g., `GetVisibleWindows`, `SetWindowPlacement`, `GetWorkAreaForRect`), strictly using pure C# domain structs (`WindowRect`, `WindowPlacement`) rather than native structs.
2. **The Adapter:** We implement `WindowsEnvironmentAdapter`, which encapsulates all `DllImport` definitions and handles the native marshalling.
3. **The Deep Module:** `WindowRestorer` accepts `IWindowingEnvironment` via its constructor. It absorbs the entire orchestration loop (matching, clamping, pinning, restoring) and drives the adapter.

## Consequences

- **Positive:** `WindowRestorer` can now be fully unit-tested in-memory using a fake `IWindowingEnvironment`, giving us a clean test surface for complex restoration scenarios (like tray restoration or cross-monitor boundary rules).
- **Positive:** Domain concepts (like `WindowRect`) are decoupled from Win32 memory layouts.
- **Negative:** We must maintain mapping code between our domain structs and the native structs inside the adapter.
- **Neutral:** For the time being, the Capture pipeline (`HandleSaveCommand` in `Program.cs`) still uses direct P/Invokes. It will eventually need to be refactored to use `IWindowingEnvironment` or a similar port.
