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
            public bool IsPinned { get; set; }
            public Guid DesktopId { get; set; } = Guid.Empty;
            public WindowPlacement Placement { get; set; } = new()
            {
                ShowCmd = 1,
                NormalPosition = new WindowRect(100, 100, 1100, 900)
            };
        }

        public Dictionary<long, WindowState> Windows { get; } = new();
        public WindowRect WorkArea { get; set; } = new(0, 0, 1920, 1080);
        public Dictionary<long, WindowPlacement> AppliedPlacements { get; } = new();

        public bool IsWindowAlive(long hwnd) => Windows.TryGetValue(hwnd, out var w) && w.IsAlive;

        public bool IsWindowVisible(long hwnd) => Windows.TryGetValue(hwnd, out var w) && w.IsVisible;

        public bool IsAppWindow(long hwnd) => Windows.TryGetValue(hwnd, out var w) && w.IsApp;

        public string GetWindowTitle(long hwnd) => Windows.TryGetValue(hwnd, out var w) ? w.Title : string.Empty;

        public string GetWindowProcessName(long hwnd) => Windows.TryGetValue(hwnd, out var w) ? w.ProcessName : string.Empty;

        public IEnumerable<long> GetVisibleWindows() =>
            Windows.Values.Where(w => w.IsVisible && w.IsApp).Select(w => w.Hwnd);

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

        public WindowRect GetWorkAreaForRect(WindowRect targetRect) => WorkArea;

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
        public void Pass2_RejectsLiteralFloatingWindowTitle_WhenSavedTitleDiffers()
        {
            var env = new FakeWindowingEnvironment();
            // A window titled literally "Floating Window" exists (even if IsApp was somehow true)
            env.Windows[7001] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 7001,
                ProcessName = "chrome",
                Title = "Floating Window",
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
                        Hwnd = 9999, // Stale HWND
                        ProcessName = "chrome",
                        WindowTitle = "Work Mail - Google Chrome",
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

            // Must NOT bind the browser record to "Floating Window"
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
    }
}
