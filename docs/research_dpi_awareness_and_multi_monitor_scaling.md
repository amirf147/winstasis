# 🔬 Systems Audit: Multi-Monitor DPI Scaling & Coordinate Virtualization in WinStasis

> **Target Repository:** `WinStasis` (`winst`)  
> **Topic:** Multi-Monitor DPI Awareness, Win32 Coordinate Virtualization, and `WINDOWPLACEMENT` Drift  
> **Status:** **Identified Issue (Deferred / Backlogged for Future Milestone)**  
> **Severity:** High (Affects multi-monitor systems with heterogeneous DPI scaling)

---

## 1. Executive Summary & Root Vulnerability

An audit of `WinStasis` confirms that **WinStasis currently suffers from the Windows DPI Virtualization Trap** on multi-monitor workstations with mixed scaling factors (e.g., 4K Primary Display at 150% DPI + 1080p Secondary Display at 100% DPI).

### The Underlying Cause
1. `WinStasis.csproj` does not embed an `app.manifest` declaring Windows DPI awareness.
2. `Program.cs` does not call `SetProcessDpiAwarenessContext`.
3. By default, the .NET runtime defaults the `winst.exe` process to **DPI Unaware** or **System DPI Aware**.

---

## 2. Failure Modes in Multi-Monitor Environments

When `winst.exe` runs in a DPI-unaware or System-DPI-aware state on a multi-monitor system with different scale factors:

### Failure 1: `GetWindowPlacement` / `SetWindowPlacement` Coordinate Drift
* In `WindowsEnvironmentAdapter.cs`, `winst` calls Win32 `GetWindowPlacement` to capture `rcNormalPosition` (`X`, `Y`, `Width`, `Height`).
* When querying a window located on a secondary display with a different DPI scale factor, Windows User32 **virtualizes and rescales the rectangle coordinates** to match the primary display's scaling.
* When `winst restore` later passes these virtualized coordinates to `SetWindowPlacement`, the window will either **shrink**, **expand exponentially**, or **offset away from its original physical position**.

### Failure 2: Boundary Clamping & Work Area Distortion
* In `WindowRestorer.cs`, `ClampToNearestMonitor()` calls `_env.GetWorkAreaForRect()` which invokes `MonitorFromRect` and `GetMonitorInfo`.
* Because the input rectangle is virtualized, `MonitorFromRect` can resolve to the wrong physical monitor handle (`HMONITOR`).
* `monitorInfo.rcWork` returns virtualized screen bounds, causing boundary clamping to snap windows to incorrect display coordinates.

---

## 3. Resolution Plan (Minimal 2-Step Remediation)

When scheduling the fix for `WinStasis`, the remediation requires two simple steps:

### Step 1: Create `WinStasis/app.manifest`
Create an application manifest in the `WinStasis/` project folder:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="WinStasis.app"/>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2, PerMonitor</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

### Step 2: Update `WinStasis/WinStasis.csproj`
Add the `<ApplicationManifest>` property to `<PropertyGroup>`:

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <AssemblyName>winst</AssemblyName>
  <ApplicationManifest>app.manifest</ApplicationManifest>
</PropertyGroup>
```

---

## 4. Current Status

This issue is documented in the WinStasis research ledger and will be implemented when multi-monitor topology features are tackled.
