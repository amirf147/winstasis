# Phase 1: The Observer (Spike Solution)
**Goal:** Prove that we can reliably extract window data from the Windows API using C#.

## 1. Objective
Create a standalone C# console application that enumerates all open windows on the system, filters out the invisible "junk," and prints the relevant details to the console.

## 2. Required Win32 API Calls (P/Invoke)
The application will need to import the following from `User32.dll`:
* `EnumWindows`: To loop through every top-level window.
* `IsWindowVisible`: To filter out hidden system processes.
* `GetWindowThreadProcessId`: To link a window to its executable name.
* `GetWindowText`: To read the title of the window.
* `GetWindowRect`: To get the X, Y, Width, and Height.

## 3. Success Criteria
Running the application should output a clean, readable list in the console. 
Crucially, the user must be able to visually identify their accessibility HUD in the console output and note its specific "Process Name" and "Window Title" for future targeting.