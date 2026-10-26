using System;
using System.Collections.Generic;
using System.Linq;
using WinStasis;
using WinStasis.Interfaces;
using WinStasis.Models;
using Xunit;

namespace WinStasis.Tests
{
    public class FakeWindowingEnvironment : IWindowingEnvironment
    {
        public class WindowState
        {
            public long Hwnd { get; set; }
            public bool IsAlive { get; set; } = true;
            public bool IsVisible { get; set; } = true;
            public bool IsApp { get; set; } = true;
            public string Title { get; set; } = string.Empty;
            public string ProcessName { get; set; } = string.Empty;
            public string ClassName { get; set; } = "TestWindow";
            public long Style { get; set; } = 0;
            public long ExStyle { get; set; } = 0;
            public bool IsPinned { get; set; }
            public Guid DesktopId { get; set; } = Guid.Empty;
            public WindowPlacement Placement { get; set; } = new()
            {
                ShowCmd = 1,
                NormalPosition = new WindowRect(100, 100, 1100, 900)
            };
            public WindowRect? WindowBounds { get; set; }
            public WindowRect? VisibleBounds { get; set; }
            public WindowFrameMargins? FrameMargins { get; set; }
        }

        public Dictionary<long, WindowState> Windows { get; } = new();
        public WindowRect WorkArea { get; set; } = new(0, 0, 1920, 1080);
        public Func<WindowRect, WindowRect>? WorkAreaLookup { get; set; }
        public Dictionary<long, WindowPlacement> AppliedPlacements { get; } = new();
        public Dictionary<long, WindowRect> AppliedPositions { get; } = new();
        public Dictionary<Guid, int> DesktopNumbers { get; } = new();

        public bool IsWindowAlive(long hwnd) => Windows.TryGetValue(hwnd, out var w) && w.IsAlive;

        public bool IsWindowVisible(long hwnd) => Windows.TryGetValue(hwnd, out var w) && w.IsVisible;

        public bool IsAppWindow(long hwnd) => Windows.TryGetValue(hwnd, out var w) && w.IsApp;

        public string GetWindowTitle(long hwnd) => Windows.TryGetValue(hwnd, out var w) ? w.Title : string.Empty;

        public string GetWindowProcessName(long hwnd) => Windows.TryGetValue(hwnd, out var w) ? w.ProcessName : string.Empty;

        public IEnumerable<long> GetVisibleWindows() =>
            Windows.Values.Where(w => w.IsVisible && w.IsApp).Select(w => w.Hwnd);

        public Guid GetWindowDesktopId(long hwnd) =>
            Windows.TryGetValue(hwnd, out var w) ? w.DesktopId : Guid.Empty;

        public int GetDesktopNumber(Guid desktopId) =>
            DesktopNumbers.TryGetValue(desktopId, out int num) ? num : 1;

        public bool IsWindowPinned(long hwnd) => Windows.TryGetValue(hwnd, out var w) && w.IsPinned;

        public void PinWindow(long hwnd)
        {
            if (Windows.TryGetValue(hwnd, out var w)) w.IsPinned = true;
        }

        public void UnpinWindow(long hwnd)
        {
            if (Windows.TryGetValue(hwnd, out var w)) w.IsPinned = false;
        }

        public bool MoveWindowToDesktop(long hwnd, Guid targetDesktopId)
        {
            if (Windows.TryGetValue(hwnd, out var w))
            {
                w.DesktopId = targetDesktopId;
                return true;
            }
            return false;
        }

        public WindowRect GetWorkAreaForRect(WindowRect targetRect) => WorkAreaLookup?.Invoke(targetRect) ?? WorkArea;

        public WindowPlacement GetWindowPlacement(long hwnd)
        {
            if (Windows.TryGetValue(hwnd, out var w))
            {
                return new WindowPlacement
                {
                    ShowCmd = w.Placement.ShowCmd,
                    NormalPosition = new WindowRect(
                        w.Placement.NormalPosition.Left,
                        w.Placement.NormalPosition.Top,
                        w.Placement.NormalPosition.Right,
                        w.Placement.NormalPosition.Bottom
                    )
                };
            }
            return new WindowPlacement { ShowCmd = 1, NormalPosition = new WindowRect(0, 0, 800, 600) };
        }

        public bool SetWindowPlacement(long hwnd, WindowPlacement placement)
        {
            if (Windows.TryGetValue(hwnd, out var w))
            {
                w.Placement = placement;
                AppliedPlacements[hwnd] = placement;
                return true;
            }
            return false;
        }

        public WindowRect GetWindowBounds(long hwnd)
        {
            if (Windows.TryGetValue(hwnd, out var w))
            {
                return w.WindowBounds ?? w.Placement.NormalPosition;
            }
            return new WindowRect(0, 0, 0, 0);
        }

        public WindowRect GetVisibleWindowBounds(long hwnd)
        {
            if (Windows.TryGetValue(hwnd, out var w))
            {
                return w.VisibleBounds ?? GetWindowBounds(hwnd);
            }
            return new WindowRect(0, 0, 0, 0);
        }

        public WindowFrameMargins GetFrameMargins(long hwnd)
        {
            if (Windows.TryGetValue(hwnd, out var w))
            {
                if (w.FrameMargins.HasValue)
                {
                    return w.FrameMargins.Value;
                }

                if (w.WindowBounds.HasValue && w.VisibleBounds.HasValue)
                {
                    WindowRect win = w.WindowBounds.Value;
                    WindowRect dwm = w.VisibleBounds.Value;
                    return new WindowFrameMargins(
                        dwm.Left - win.Left,
                        dwm.Top - win.Top,
                        win.Right - dwm.Right,
                        win.Bottom - dwm.Bottom
                    );
                }
            }
            return new WindowFrameMargins(0, 0, 0, 0);
        }

        public bool SetWindowPosition(long hwnd, WindowRect win32Rect)
        {
            if (Windows.TryGetValue(hwnd, out var w))
            {
                w.WindowBounds = win32Rect;
                AppliedPositions[hwnd] = win32Rect;
                return true;
            }
            return false;
        }

        public WindowDiagnostics GetWindowDiagnostics(long hwnd)
        {
            if (Windows.TryGetValue(hwnd, out var w))
            {
                return new WindowDiagnostics
                {
                    ClassName = w.ClassName,
                    Style = w.Style,
                    ExStyle = w.ExStyle,
                    RawWin32Rect = GetWindowBounds(hwnd),
                    VisibleDwmRect = GetVisibleWindowBounds(hwnd),
                    NormalPositionRect = GetWindowPlacement(hwnd).NormalPosition,
                    FrameMargins = GetFrameMargins(hwnd)
                };
            }
            return new WindowDiagnostics();
        }
    }

    public class WindowRestorerTests
    {
        [Fact]
        public void Pass1_ExactMatch_RestoresSavedWindow()
        {
            var env = new FakeWindowingEnvironment();
            env.Windows[1001] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 1001,
                ProcessName = "chrome",
                Title = "GitHub - Google Chrome",
                Placement = new WindowPlacement { ShowCmd = 1, NormalPosition = new WindowRect(50, 50, 1050, 850) }
            };

            var profile = new SessionProfile
            {
                ProfileName = "test",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 1001,
                        ProcessName = "chrome",
                        WindowTitle = "GitHub - Google Chrome",
                        X = 50,
                        Y = 50,
                        Width = 1000,
                        Height = 800,
                        ShowCmd = 1
                    }
                }
            };

            var restorer = new WindowRestorer(env);
            restorer.Restore(profile);

            Assert.True(env.AppliedPlacements.ContainsKey(1001));
            Assert.Equal(1, env.AppliedPlacements[1001].ShowCmd);
            Assert.Equal(50, env.AppliedPlacements[1001].NormalPosition.Left);
            Assert.Equal(1050, env.AppliedPlacements[1001].NormalPosition.Right);
        }

        [Fact]
        public void Pass2_TitleDrift_MatchesByProcessName()
        {
            var env = new FakeWindowingEnvironment();
            // App mutated title after reboot and got a new HWND
            env.Windows[2002] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 2002,
                ProcessName = "obsidian",
                Title = "Daily Note 2026-10-10 - Obsidian",
                Placement = new WindowPlacement { ShowCmd = 1, NormalPosition = new WindowRect(100, 100, 900, 700) }
            };

            var profile = new SessionProfile
            {
                ProfileName = "test",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 9999, // stale HWND
                        ProcessName = "obsidian",
                        WindowTitle = "Welcome - Obsidian",
                        X = 150,
                        Y = 150,
                        Width = 800,
                        Height = 600,
                        ShowCmd = 1
                    }
                }
            };

            var restorer = new WindowRestorer(env);
            restorer.Restore(profile);

            Assert.True(env.AppliedPlacements.ContainsKey(2002));
            Assert.Equal(150, env.AppliedPlacements[2002].NormalPosition.Left);
            Assert.Equal(950, env.AppliedPlacements[2002].NormalPosition.Right);
        }

        [Fact]
        public void Pass2_IgnoresFloatingToolWindows_AndBindsToMainApplicationWindow()
        {
            var env = new FakeWindowingEnvironment();
            // App has an internal auxiliary window titled "Floating Window"
            env.Windows[3001] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 3001,
                ProcessName = "RemoteDesktopManager",
                Title = "Floating Window",
                IsApp = false // Tool window
            };
            // And its main application window
            env.Windows[3002] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 3002,
                ProcessName = "RemoteDesktopManager",
                Title = "Remote Desktop Manager 2026.2",
                IsApp = true
            };

            var profile = new SessionProfile
            {
                ProfileName = "test",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 8888, // stale HWND
                        ProcessName = "RemoteDesktopManager",
                        WindowTitle = "Remote Desktop Manager 2026.1", // drifted title
                        X = 0,
                        Y = 0,
                        Width = 1600,
                        Height = 900,
                        ShowCmd = 1
                    }
                }
            };

            var restorer = new WindowRestorer(env);
            restorer.Restore(profile);

            // Must NOT claim the tool window 3001
            Assert.False(env.AppliedPlacements.ContainsKey(3001));
            // Must claim the real application window 3002
            Assert.True(env.AppliedPlacements.ContainsKey(3002));
            Assert.Equal(1600, env.AppliedPlacements[3002].NormalPosition.Width);
        }

        [Fact]
        public void Restore_MinimizedWindow_WithCleanRestoredBounds_SetsNormalPosition()
        {
            var env = new FakeWindowingEnvironment();
            env.Windows[4001] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 4001,
                ProcessName = "discord",
                Title = "Discord",
                Placement = new WindowPlacement
                {
                    ShowCmd = 2,
                    NormalPosition = new WindowRect(200, 200, 1400, 1000)
                }
            };

            var profile = new SessionProfile
            {
                ProfileName = "test",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 4001,
                        ProcessName = "discord",
                        WindowTitle = "Discord",
                        X = 200,
                        Y = 200,
                        Width = 1200,
                        Height = 800,
                        ShowCmd = 2 // Saved minimized
                    }
                }
            };

            var restorer = new WindowRestorer(env);
            restorer.Restore(profile);

            Assert.True(env.AppliedPlacements.ContainsKey(4001));
            Assert.Equal(2, env.AppliedPlacements[4001].ShowCmd);
            Assert.Equal(200, env.AppliedPlacements[4001].NormalPosition.Left);
            Assert.Equal(1200, env.AppliedPlacements[4001].NormalPosition.Width);
            Assert.Equal(800, env.AppliedPlacements[4001].NormalPosition.Height);
        }

        [Fact]
        public void Restore_LegacyCorruptedMinimizedRecord_PreservesLiveNormalPosition()
        {
            var env = new FakeWindowingEnvironment();
            // App was started minimized; OS holds its valid normal restored rect
            env.Windows[5001] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 5001,
                ProcessName = "xyplorer",
                Title = "XYplorer 28.40",
                Placement = new WindowPlacement
                {
                    ShowCmd = 2,
                    NormalPosition = new WindowRect(100, 100, 1300, 900) // 1200x800
                }
            };

            // Legacy pre-fix profile containing corrupted minimized placeholder rect (-32000, 160x28)
            var profile = new SessionProfile
            {
                ProfileName = "test",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 5001,
                        ProcessName = "xyplorer",
                        WindowTitle = "XYplorer 28.40",
                        X = -32000,
                        Y = -32000,
                        Width = 160,
                        Height = 28,
                        ShowCmd = 2
                    }
                }
            };

            var restorer = new WindowRestorer(env);
            restorer.Restore(profile);

            Assert.True(env.AppliedPlacements.ContainsKey(5001));
            Assert.Equal(2, env.AppliedPlacements[5001].ShowCmd);
            // NormalPosition must NOT be crushed into a 160x28 box; it should preserve the live 1200x800 size
            Assert.Equal(1200, env.AppliedPlacements[5001].NormalPosition.Width);
            Assert.Equal(800, env.AppliedPlacements[5001].NormalPosition.Height);
        }

        [Fact]
        public void Restore_TargetFlag_ContextuallyUnminimizesWindow()
        {
            var env = new FakeWindowingEnvironment();
            env.Windows[6001] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 6001,
                ProcessName = "terminal",
                Title = "Windows Terminal",
                Placement = new WindowPlacement
                {
                    ShowCmd = 2,
                    NormalPosition = new WindowRect(100, 100, 1100, 800)
                }
            };

            var profile = new SessionProfile
            {
                ProfileName = "test",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 6001,
                        ProcessName = "terminal",
                        WindowTitle = "Windows Terminal",
                        X = 100,
                        Y = 100,
                        Width = 1000,
                        Height = 700,
                        ShowCmd = 2
                    }
                }
            };

            var restorer = new WindowRestorer(env);
            // Restore with target ID specified: ADR-0004 specifies ShowCmd 2 is overridden to 1
            restorer.Restore(profile, targetId: 1);

            Assert.True(env.AppliedPlacements.ContainsKey(6001));
            Assert.Equal(1, env.AppliedPlacements[6001].ShowCmd);
        }

        [Fact]
        public void Pass1_RejectsHwnd_WhenWindowIsNotAppWindow()
        {
            var env = new FakeWindowingEnvironment();
            // Window exists with same process name and same HWND, but is a non-app tool/overlay window
            env.Windows[7001] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 7001,
                ProcessName = "max",
                Title = "Floating Window",
                IsApp = false // E.g. push-to-talk volume pill overlay
            };

            var profile = new SessionProfile
            {
                ProfileName = "test",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 7001, // Points to the non-app window handle
                        ProcessName = "max",
                        WindowTitle = "Brain MAX",
                        X = 100,
                        Y = 100,
                        Width = 1000,
                        Height = 700,
                        ShowCmd = 1
                    }
                }
            };

            var restorer = new WindowRestorer(env);
            restorer.Restore(profile);

            // Pass 1 must NOT claim 7001 because it is not an application window
            Assert.False(env.AppliedPlacements.ContainsKey(7001));
        }

        [Fact]
        public void Restore_IgnoresLegacyAuxiliaryWidgetRecords()
        {
            var env = new FakeWindowingEnvironment();
            // A legitimate running app window
            env.Windows[7002] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 7002,
                ProcessName = "max",
                Title = "Brain MAX",
                IsApp = true
            };
            // An auxiliary tool/overlay window running alongside it
            env.Windows[7001] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 7001,
                ProcessName = "max",
                Title = "Floating Window",
                IsApp = false
            };

            // Legacy profile containing both the main application record and the auxiliary floating pill record
            var profile = new SessionProfile
            {
                ProfileName = "test",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 7002,
                        ProcessName = "max",
                        WindowTitle = "Brain MAX",
                        X = 100,
                        Y = 100,
                        Width = 1200,
                        Height = 800,
                        ShowCmd = 1
                    },
                    new WindowRecord
                    {
                        TargetId = 2,
                        Hwnd = 7001,
                        ProcessName = "max",
                        WindowTitle = "Floating Window",
                        X = 944,
                        Y = 1148,
                        Width = 32,
                        Height = 20,
                        ShowCmd = 1
                    }
                }
            };

            var restorer = new WindowRestorer(env);
            restorer.Restore(profile);

            // Main app 7002 is restored successfully
            Assert.True(env.AppliedPlacements.ContainsKey(7002));
            // Auxiliary tool/overlay 7001 is NEVER touched or placed
            Assert.False(env.AppliedPlacements.ContainsKey(7001));
        }

        [Fact]
        public void Pass2_ExclusiveAllocation_EnforcesOneToOneHandleAssignment()
        {
            var env = new FakeWindowingEnvironment();
            // Two open windows of same process
            env.Windows[8001] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 8001,
                ProcessName = "code",
                Title = "File1.cs - VS Code",
                IsApp = true
            };
            env.Windows[8002] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 8002,
                ProcessName = "code",
                Title = "File2.cs - VS Code",
                IsApp = true
            };

            var profile = new SessionProfile
            {
                ProfileName = "test",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 9001, // Stale HWND
                        ProcessName = "code",
                        WindowTitle = "OldTitle1 - VS Code",
                        X = 0,
                        Y = 0,
                        Width = 800,
                        Height = 600,
                        ShowCmd = 1
                    },
                    new WindowRecord
                    {
                        TargetId = 2,
                        Hwnd = 9002, // Stale HWND
                        ProcessName = "code",
                        WindowTitle = "OldTitle2 - VS Code",
                        X = 800,
                        Y = 0,
                        Width = 800,
                        Height = 600,
                        ShowCmd = 1
                    }
                }
            };

            var restorer = new WindowRestorer(env);
            restorer.Restore(profile);

            // Both windows must be claimed, and both must receive distinct handles
            Assert.True(env.AppliedPlacements.ContainsKey(8001));
            Assert.True(env.AppliedPlacements.ContainsKey(8002));
            Assert.Equal(2, env.AppliedPlacements.Count);
        }

        [Fact]
        public void Restore_SnappedWindow_AppliesDwmFrameMarginCompensation_ViaSetWindowPosition()
        {
            var env = new FakeWindowingEnvironment();
            env.Windows[9101] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 9101,
                ProcessName = "notepad",
                Title = "Untitled - Notepad",
                FrameMargins = new WindowFrameMargins(7, 0, 7, 7),
                Placement = new WindowPlacement
                {
                    ShowCmd = 1,
                    NormalPosition = new WindowRect(100, 100, 800, 600)
                }
            };

            var profile = new SessionProfile
            {
                ProfileName = "test-snapped",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 9101,
                        ProcessName = "notepad",
                        WindowTitle = "Untitled - Notepad",
                        X = 0,
                        Y = 0,
                        Width = 960,
                        Height = 1040,
                        ShowCmd = 1
                    }
                }
            };

            var restorer = new WindowRestorer(env);
            restorer.Restore(profile);

            Assert.True(env.AppliedPlacements.ContainsKey(9101));
            Assert.Equal(0, env.AppliedPlacements[9101].NormalPosition.Left);
            Assert.Equal(0, env.AppliedPlacements[9101].NormalPosition.Top);
            Assert.Equal(960, env.AppliedPlacements[9101].NormalPosition.Right);
            Assert.Equal(1040, env.AppliedPlacements[9101].NormalPosition.Bottom);

            Assert.True(env.AppliedPositions.ContainsKey(9101));
            Assert.Equal(-7, env.AppliedPositions[9101].Left);
            Assert.Equal(0, env.AppliedPositions[9101].Top);
            Assert.Equal(967, env.AppliedPositions[9101].Right);
            Assert.Equal(1047, env.AppliedPositions[9101].Bottom);
            Assert.Equal(974, env.AppliedPositions[9101].Width);
            Assert.Equal(1047, env.AppliedPositions[9101].Height);
        }

        [Fact]
        public void Restore_AdjacentSnappedWindows_AlignVisibleBordersFlush_WithZeroOverlap()
        {
            var env = new FakeWindowingEnvironment();
            env.WorkArea = new WindowRect(0, 0, 1920, 1040);

            env.Windows[9201] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 9201,
                ProcessName = "code",
                Title = "VS Code",
                FrameMargins = new WindowFrameMargins(7, 0, 7, 7),
                Placement = new WindowPlacement { ShowCmd = 1, NormalPosition = new WindowRect(50, 50, 800, 600) }
            };

            env.Windows[9202] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 9202,
                ProcessName = "terminal",
                Title = "Terminal",
                FrameMargins = new WindowFrameMargins(7, 0, 7, 7),
                Placement = new WindowPlacement { ShowCmd = 1, NormalPosition = new WindowRect(50, 50, 800, 600) }
            };

            var profile = new SessionProfile
            {
                ProfileName = "test-adjacent",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 9201,
                        ProcessName = "code",
                        WindowTitle = "VS Code",
                        X = 0,
                        Y = 0,
                        Width = 960,
                        Height = 1040,
                        ShowCmd = 1
                    },
                    new WindowRecord
                    {
                        TargetId = 2,
                        Hwnd = 9202,
                        ProcessName = "terminal",
                        WindowTitle = "Terminal",
                        X = 960,
                        Y = 0,
                        Width = 960,
                        Height = 1040,
                        ShowCmd = 1
                    }
                }
            };

            var restorer = new WindowRestorer(env);
            restorer.Restore(profile);

            Assert.True(env.AppliedPositions.ContainsKey(9201));
            Assert.True(env.AppliedPositions.ContainsKey(9202));

            var win1 = env.AppliedPositions[9201];
            var win2 = env.AppliedPositions[9202];

            Assert.Equal(new WindowRect(-7, 0, 967, 1047), win1);
            Assert.Equal(new WindowRect(953, 0, 1927, 1047), win2);

            int win1VisibleRight = win1.Right - 7;
            int win2VisibleLeft = win2.Left + 7;
            Assert.Equal(960, win1VisibleRight);
            Assert.Equal(960, win2VisibleLeft);
            Assert.Equal(0, win1VisibleRight - win2VisibleLeft);
        }

        [Fact]
        public void Restore_EndToEnd_SnappedUltrawideWindow_RestoresActiveColumn_NotStaleNormalPosition()
        {
            var env = new FakeWindowingEnvironment();
            env.WorkArea = new WindowRect(0, 0, 5120, 1400);

            env.Windows[9301] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 9301,
                ProcessName = "browser",
                Title = "Browser - Ultrawide",
                VisibleBounds = new WindowRect(1706, 0, 3413, 1400),
                WindowBounds = new WindowRect(1699, 0, 3420, 1407),
                Placement = new WindowPlacement
                {
                    ShowCmd = 1,
                    NormalPosition = new WindowRect(200, 200, 1400, 1000)
                }
            };

            var capturer = new WindowCapturer(env);
            var profile = capturer.Capture("ultrawide");

            // Simulate window moved away
            env.Windows[9301].VisibleBounds = new WindowRect(100, 100, 900, 700);
            env.Windows[9301].WindowBounds = new WindowRect(93, 100, 907, 707);

            var restorer = new WindowRestorer(env);
            restorer.Restore(profile);

            Assert.True(env.AppliedPlacements.ContainsKey(9301));
            Assert.Equal(1706, env.AppliedPlacements[9301].NormalPosition.Left);
            Assert.Equal(1707, env.AppliedPlacements[9301].NormalPosition.Width);
            Assert.Equal(1400, env.AppliedPlacements[9301].NormalPosition.Height);

            Assert.True(env.AppliedPositions.ContainsKey(9301));
            Assert.Equal(new WindowRect(1699, 0, 3420, 1407), env.AppliedPositions[9301]);
        }

        [Fact]
        public void Restore_UltrawideScreenCoordinates_ClampsWithinSmallerMonitorWorkArea()
        {
            var env = new FakeWindowingEnvironment();
            env.WorkArea = new WindowRect(0, 0, 1920, 1040);

            env.Windows[9401] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 9401,
                ProcessName = "slack",
                Title = "Slack",
                FrameMargins = new WindowFrameMargins(7, 0, 7, 7),
                Placement = new WindowPlacement
                {
                    ShowCmd = 1,
                    NormalPosition = new WindowRect(50, 50, 800, 600)
                }
            };

            var profile = new SessionProfile
            {
                ProfileName = "test-clamp",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 9401,
                        ProcessName = "slack",
                        WindowTitle = "Slack",
                        X = 3000,
                        Y = 100,
                        Width = 2400,
                        Height = 1200,
                        ShowCmd = 1
                    }
                }
            };

            var restorer = new WindowRestorer(env);
            restorer.Restore(profile);

            Assert.True(env.AppliedPlacements.ContainsKey(9401));
            Assert.Equal(new WindowRect(0, 0, 1920, 1040), env.AppliedPlacements[9401].NormalPosition);

            Assert.True(env.AppliedPositions.ContainsKey(9401));
            Assert.Equal(new WindowRect(-7, 0, 1927, 1047), env.AppliedPositions[9401]);
        }

        [Fact]
        public void Restore_VerboseFlag_OutputsDiagnosticTraces()
        {
            var env = new FakeWindowingEnvironment();
            env.Windows[9501] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 9501,
                ProcessName = "wpfapp",
                Title = "WPF Application",
                ClassName = "WpfWindowClass",
                FrameMargins = new WindowFrameMargins(7, 0, 7, 7),
                Placement = new WindowPlacement
                {
                    ShowCmd = 1,
                    NormalPosition = new WindowRect(50, 50, 850, 650)
                }
            };

            var profile = new SessionProfile
            {
                ProfileName = "test-verbose",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 9501,
                        ProcessName = "wpfapp",
                        WindowTitle = "WPF Application",
                        X = 50,
                        Y = 50,
                        Width = 800,
                        Height = 600,
                        ShowCmd = 1
                    }
                }
            };

            var restorer = new WindowRestorer(env);
            var originalOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            try
            {
                Console.SetOut(sw);
                restorer.Restore(profile, verbose: true);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            string output = sw.ToString();
            Assert.Contains("Match: Pass 1", output);
            Assert.Contains("WpfWindowClass", output);
            Assert.Contains("Pre-Restore Bounds:", output);
            Assert.Contains("Target Clamping:", output);
            Assert.Contains("DWM Compensation:", output);
        }
    }
}
