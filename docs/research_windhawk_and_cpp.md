# Research Spike: Windhawk, C++, and Undocumented APIs

**Status:** Research

## 1. What is Windhawk?
Windhawk is a powerful customization utility created by Ramen Software (m417z). It allows developers to write "mods" that deeply customize the Windows Taskbar, Start Menu, and Explorer.

**How it works:** Instead of acting as an external script (like `winst`), Windhawk uses **DLL Injection and API Hooking**. It literally forces its own C++ code directly into the memory space of `explorer.exe`. Once inside, it intercepts (hooks) the internal functions of Windows and changes their behavior on the fly.

## 2. Does Windhawk suffer from the Windows Update problem?
**Yes, immensely.** 
Because Windhawk mods rely on undocumented functions and specific UI elements inside `explorer.exe`, they are notoriously fragile. Whenever Microsoft releases a major (and sometimes minor) Windows update, Taskbar and Explorer mods frequently break. 

Windhawk attempts to mitigate this by automatically downloading Microsoft's "Debug Symbols" (which map out memory addresses) for the specific version of Windows you are running. However, if Microsoft changes the *logic* or the *memory layout* of a COM interface, the mod author must manually rewrite the C++ code to fix it.

## 3. Does switching to C++ solve the undocumented API problem?
**No. The fragility is tied to the operating system's memory layout, not the programming language.**

Windows COM interfaces (like `IVirtualDesktopManagerInternal`) are fundamentally just a list of memory addresses called a `vtable` (Virtual Method Table). 
*   In Build 22621, `MoveViewToDesktop` might be the 7th function in the list.
*   In Build 22631, Microsoft might add a new function at position 3. 
*   Suddenly, `MoveViewToDesktop` is pushed down to the 8th position.

If an application blindly calls the 7th position, it will execute the wrong function and crash. **This happens at the raw CPU memory level.** It does not matter if you wrote the code in C#, C++, Rust, or Assembly—if the memory offset shifts, the code breaks.

### Why does Windhawk use C++?
Windhawk uses C++ because it is injecting code directly into `explorer.exe` (which is written in C++). C# runs inside the .NET Runtime, which makes it very difficult and heavy to inject directly into other system processes.

### C# vs C++ for WinStasis
For a standalone utility like `winst`, **C# is actually safer than C++**. 
*   If a C++ injected mod calls the wrong memory offset, it will instantly crash `explorer.exe` (restarting your entire Windows Taskbar). 
*   If a C# application like `winst` calls the wrong memory offset, only `winst` crashes. 

**Conclusion:** Rewriting `winst` in C++ would require significantly more boilerplate code, would be harder to maintain, and would *not* solve the OS breakage issue. Relying on a C# wrapper library (like Slion's `VirtualDesktop`) is functionally identical to what C++ developers do: relying on a massive `switch` statement that checks the OS version and applies the correct memory offsets.
