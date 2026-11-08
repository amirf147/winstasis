using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WinStasis;
using WinStasis.Matching;
using WinStasis.Models;
using Xunit;

namespace WinStasis.Tests
{
    public class HungarianSolverTests
    {
        [Fact]
        public void SolveMaxWeight_EmptyWeights_ReturnsEmpty()
        {
            double[,] weights = new double[0, 0];
            int[] result = HungarianSolver.SolveMaxWeight(weights);
            Assert.Empty(result);
        }

        [Fact]
        public void SolveMaxWeight_ZeroColumns_ReturnsAllNegativeOne()
        {
            double[,] weights = new double[3, 0];
            int[] result = HungarianSolver.SolveMaxWeight(weights);
            Assert.Equal(3, result.Length);
            Assert.All(result, col => Assert.Equal(-1, col));
        }

        [Fact]
        public void SolveMaxWeight_2x2_DiagonalDominant()
        {
            double[,] weights = new double[2, 2]
            {
                { 100.0, 10.0 },
                { 10.0, 50.0 }
            };
            int[] result = HungarianSolver.SolveMaxWeight(weights);
            Assert.Equal(new[] { 0, 1 }, result);
        }

        [Fact]
        public void SolveMaxWeight_2x2_AntiDiagonalDominant()
        {
            double[,] weights = new double[2, 2]
            {
                { 10.0, 100.0 },
                { 50.0, 10.0 }
            };
            int[] result = HungarianSolver.SolveMaxWeight(weights);
            Assert.Equal(new[] { 1, 0 }, result);
        }

        [Fact]
        public void SolveMaxWeight_MoreRowsThanColumns_AssignsExcessToNegativeOne()
        {
            double[,] weights = new double[3, 2]
            {
                { 100.0, 10.0 },
                { 10.0, 80.0 },
                { 5.0, 5.0 }
            };
            int[] result = HungarianSolver.SolveMaxWeight(weights);
            Assert.Equal(3, result.Length);
            Assert.Equal(0, result[0]);
            Assert.Equal(1, result[1]);
            Assert.Equal(-1, result[2]);
        }

        [Fact]
        public void SolveMaxWeight_MoreColumnsThanRows_AssignsAllRowsToBestDistinctColumns()
        {
            double[,] weights = new double[2, 3]
            {
                { 10.0, 100.0, 20.0 },
                { 90.0, 10.0, 15.0 }
            };
            int[] result = HungarianSolver.SolveMaxWeight(weights);
            Assert.Equal(2, result.Length);
            Assert.Equal(1, result[0]);
            Assert.Equal(0, result[1]);
        }
    }

    public class WindowMatcherTests
    {
        [Fact]
        public void MultiInstance_IdeWorkspaceDrift_Resolves4WayCycleAccurately()
        {
            var env = new FakeWindowingEnvironment();
            env.WorkArea = new WindowRect(0, 0, 1920, 1200);

            Guid desk1 = Guid.NewGuid();
            Guid desk2 = Guid.NewGuid();
            Guid desk3 = Guid.NewGuid();
            Guid desk4 = Guid.NewGuid();

            var profile = new SessionProfile
            {
                ProfileName = "ide-4way",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 1001,
                        ProcessName = "Antigravity IDE",
                        WindowTitle = "NativeMemory.cs - active-desktop-context-engine - Antigravity IDE",
                        X = 100,
                        Y = 100,
                        Width = 1200,
                        Height = 800,
                        ShowCmd = 1,
                        DesktopId = desk1
                    },
                    new WindowRecord
                    {
                        TargetId = 2,
                        Hwnd = 1002,
                        ProcessName = "Antigravity IDE",
                        WindowTitle = "VideoThumbnail.png - YouTube-Channel - Antigravity IDE",
                        X = 0,
                        Y = 0,
                        Width = 960,
                        Height = 1152,
                        ShowCmd = 1,
                        DesktopId = desk2
                    },
                    new WindowRecord
                    {
                        TargetId = 3,
                        Hwnd = 1003,
                        ProcessName = "Antigravity IDE",
                        WindowTitle = "Chapter1.mp3 - Audiobook-Project - Antigravity IDE",
                        X = 0,
                        Y = 0,
                        Width = 1920,
                        Height = 1168,
                        ShowCmd = 3,
                        DesktopId = desk3
                    },
                    new WindowRecord
                    {
                        TargetId = 4,
                        Hwnd = 1004,
                        ProcessName = "Antigravity IDE",
                        WindowTitle = "WindowCapturer.cs - WinStasis - Antigravity IDE",
                        X = 500,
                        Y = 300,
                        Width = 400,
                        Height = 270,
                        ShowCmd = 1,
                        DesktopId = desk4
                    }
                }
            };

            // Live windows are in inverted OS Z-order with drifted active files
            env.Windows[4004] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 4004,
                ProcessName = "Antigravity IDE",
                Title = "HungarianSolver.cs - WinStasis - Antigravity IDE",
                Placement = new WindowPlacement { ShowCmd = 1, NormalPosition = new WindowRect(0, 0, 800, 600) },
                VisibleBounds = new WindowRect(500, 300, 900, 570)
            };

            env.Windows[4003] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 4003,
                ProcessName = "Antigravity IDE",
                Title = "Chapter2.mp3 - Audiobook-Project - Antigravity IDE",
                Placement = new WindowPlacement { ShowCmd = 3, NormalPosition = new WindowRect(0, 0, 1920, 1168) }
            };

            env.Windows[4002] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 4002,
                ProcessName = "Antigravity IDE",
                Title = "ChannelBanner.psd - YouTube-Channel - Antigravity IDE",
                Placement = new WindowPlacement { ShowCmd = 1, NormalPosition = new WindowRect(0, 0, 800, 600) },
                VisibleBounds = new WindowRect(0, 0, 960, 1152)
            };

            env.Windows[4001] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 4001,
                ProcessName = "Antigravity IDE",
                Title = "VirtualDesktopShim.cs - active-desktop-context-engine - Antigravity IDE",
                Placement = new WindowPlacement { ShowCmd = 1, NormalPosition = new WindowRect(0, 0, 800, 600) },
                VisibleBounds = new WindowRect(100, 100, 1300, 900)
            };

            var restorer = new WindowRestorer(env);
            restorer.Restore(profile);

            // WinStasis workspace receives 400x270 and desk4
            Assert.True(env.AppliedPlacements.ContainsKey(4004));
            Assert.Equal(400, env.AppliedPlacements[4004].NormalPosition.Width);
            Assert.Equal(270, env.AppliedPlacements[4004].NormalPosition.Height);
            Assert.Equal(desk4, env.Windows[4004].DesktopId);

            // Audiobook-Project workspace receives 1920x1168, ShowCmd 3, and desk3
            Assert.True(env.AppliedPlacements.ContainsKey(4003));
            Assert.Equal(3, env.AppliedPlacements[4003].ShowCmd);
            Assert.Equal(1920, env.AppliedPlacements[4003].NormalPosition.Width);
            Assert.Equal(1168, env.AppliedPlacements[4003].NormalPosition.Height);
            Assert.Equal(desk3, env.Windows[4003].DesktopId);

            // YouTube-Channel workspace receives 960x1152 and desk2
            Assert.True(env.AppliedPlacements.ContainsKey(4002));
            Assert.Equal(960, env.AppliedPlacements[4002].NormalPosition.Width);
            Assert.Equal(1152, env.AppliedPlacements[4002].NormalPosition.Height);
            Assert.Equal(desk2, env.Windows[4002].DesktopId);

            // active-desktop-context-engine workspace receives 1200x800 and desk1
            Assert.True(env.AppliedPlacements.ContainsKey(4001));
            Assert.Equal(1200, env.AppliedPlacements[4001].NormalPosition.Width);
            Assert.Equal(800, env.AppliedPlacements[4001].NormalPosition.Height);
            Assert.Equal(desk1, env.Windows[4001].DesktopId);
        }

        [Fact]
        public void MultiInstance_MaximizedBrowsersWithIdenticalBounds_MatchesByTabStripOverlap()
        {
            var env = new FakeWindowingEnvironment();

            Guid desk1 = Guid.NewGuid();
            Guid desk2 = Guid.NewGuid();
            Guid desk3 = Guid.NewGuid();

            var profile = new SessionProfile
            {
                ProfileName = "waterfox-tabs",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 2001,
                        ProcessName = "waterfox",
                        WindowTitle = "OSnya / OSnya · GitLab - Waterfox",
                        Tabs = new[] { "OSnya / OSnya · GitLab", "GitLab CI Pipeline", "Merge Request #4" },
                        X = 0,
                        Y = 0,
                        Width = 1920,
                        Height = 1168,
                        ShowCmd = 3,
                        DesktopId = desk1
                    },
                    new WindowRecord
                    {
                        TargetId = 2,
                        Hwnd = 2002,
                        ProcessName = "waterfox",
                        WindowTitle = "deepseek-ai/DeepSeek-V3 - Waterfox",
                        Tabs = new[] { "deepseek-ai/DeepSeek-V3", "Models - DeepSeek", "API Documentation" },
                        X = 0,
                        Y = 0,
                        Width = 1920,
                        Height = 1168,
                        ShowCmd = 3,
                        DesktopId = desk2
                    },
                    new WindowRecord
                    {
                        TargetId = 3,
                        Hwnd = 2003,
                        ProcessName = "waterfox",
                        WindowTitle = "Problem loading page - Waterfox",
                        Tabs = new[] { "Problem loading page", "Hugging Face - Models", "PyTorch Forums" },
                        X = 0,
                        Y = 0,
                        Width = 1920,
                        Height = 1168,
                        ShowCmd = 3,
                        DesktopId = desk3
                    }
                }
            };

            // 3 live windows: identical ShowCmd=3 and normal position, drifted active tab titles
            env.Windows[5001] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 5001,
                ProcessName = "waterfox",
                Title = "Google AI Studio - Waterfox",
                Tabs = new[] { "Google AI Studio", "GitLab CI Pipeline", "Merge Request #4", "OSnya / OSnya · GitLab" },
                Placement = new WindowPlacement { ShowCmd = 3, NormalPosition = new WindowRect(0, 0, 1920, 1168) }
            };

            env.Windows[5002] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 5002,
                ProcessName = "waterfox",
                Title = "Muse-Glimmer - Waterfox",
                Tabs = new[] { "Muse-Glimmer", "deepseek-ai/DeepSeek-V3", "API Documentation" },
                Placement = new WindowPlacement { ShowCmd = 3, NormalPosition = new WindowRect(0, 0, 1920, 1168) }
            };

            env.Windows[5003] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 5003,
                ProcessName = "waterfox",
                Title = "Models - Hugging Face - Waterfox", // Note "Models" token collision with Target 2's "Models - DeepSeek"
                Tabs = new[] { "Models - Hugging Face", "Hugging Face - Models", "PyTorch Forums" },
                Placement = new WindowPlacement { ShowCmd = 3, NormalPosition = new WindowRect(0, 0, 1920, 1168) }
            };

            var restorer = new WindowRestorer(env);
            restorer.Restore(profile);

            Assert.True(env.AppliedPlacements.ContainsKey(5001));
            Assert.Equal(desk1, env.Windows[5001].DesktopId);

            Assert.True(env.AppliedPlacements.ContainsKey(5002));
            Assert.Equal(desk2, env.Windows[5002].DesktopId);

            Assert.True(env.AppliedPlacements.ContainsKey(5003));
            Assert.Equal(desk3, env.Windows[5003].DesktopId);
        }

        [Fact]
        public void MultiInstance_OneWindowCompletelyMutated_ResolvedByElimination()
        {
            var env = new FakeWindowingEnvironment();

            Guid desk1 = Guid.NewGuid();
            Guid desk2 = Guid.NewGuid();
            Guid desk3 = Guid.NewGuid();

            var profile = new SessionProfile
            {
                ProfileName = "elimination",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 3001,
                        ProcessName = "chrome",
                        WindowTitle = "Work Dashboard - Chrome",
                        Tabs = new[] { "Dashboard", "Analytics" },
                        X = 0,
                        Y = 0,
                        Width = 800,
                        Height = 600,
                        ShowCmd = 1,
                        DesktopId = desk1
                    },
                    new WindowRecord
                    {
                        TargetId = 2,
                        Hwnd = 3002,
                        ProcessName = "chrome",
                        WindowTitle = "Pull Requests - Chrome",
                        Tabs = new[] { "PR #1", "PR #2" },
                        X = 800,
                        Y = 0,
                        Width = 800,
                        Height = 600,
                        ShowCmd = 1,
                        DesktopId = desk2
                    },
                    new WindowRecord
                    {
                        TargetId = 3,
                        Hwnd = 3003,
                        ProcessName = "chrome",
                        WindowTitle = "Internal Wiki - Chrome",
                        Tabs = new[] { "Wiki Home", "Architecture" },
                        X = 400,
                        Y = 400,
                        Width = 800,
                        Height = 600,
                        ShowCmd = 1,
                        DesktopId = desk3
                    }
                }
            };

            // Window 6003 had every tab and title replaced (0.0 similarity to all records)
            env.Windows[6001] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 6001,
                ProcessName = "chrome",
                Title = "Analytics Overview - Chrome",
                Tabs = new[] { "Dashboard", "Analytics" },
                Placement = new WindowPlacement { ShowCmd = 1, NormalPosition = new WindowRect(0, 0, 800, 600) }
            };

            env.Windows[6002] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 6002,
                ProcessName = "chrome",
                Title = "Open Reviews - Chrome",
                Tabs = new[] { "PR #1", "PR #2" },
                Placement = new WindowPlacement { ShowCmd = 1, NormalPosition = new WindowRect(800, 0, 1600, 600) }
            };

            env.Windows[6003] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 6003,
                ProcessName = "chrome",
                Title = "Random Cat Videos - YouTube - Chrome",
                Tabs = new[] { "Video 1", "Video 2" },
                Placement = new WindowPlacement { ShowCmd = 1, NormalPosition = new WindowRect(100, 100, 900, 700) }
            };

            var restorer = new WindowRestorer(env);
            restorer.Restore(profile);

            Assert.True(env.AppliedPlacements.ContainsKey(6001));
            Assert.Equal(desk1, env.Windows[6001].DesktopId);

            Assert.True(env.AppliedPlacements.ContainsKey(6002));
            Assert.Equal(desk2, env.Windows[6002].DesktopId);

            // Window 6003 must be assigned to Target 3 by elimination
            Assert.True(env.AppliedPlacements.ContainsKey(6003));
            Assert.Equal(desk3, env.Windows[6003].DesktopId);
            Assert.Equal(400, env.AppliedPlacements[6003].NormalPosition.Left);
        }

        [Fact]
        public void Stage1_KernelInstanceToken_MatchesDespiteFullTitleAndTabMutation()
        {
            var env = new FakeWindowingEnvironment();
            const long testToken = 0x123456789ABCDEFL;

            var profile = new SessionProfile
            {
                ProfileName = "token-test",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 9999, // Stale HWND
                        InstanceToken = testToken,
                        ProcessName = "notepad",
                        WindowTitle = "SecretPlan.txt - Notepad",
                        Tabs = new[] { "SecretPlan.txt" },
                        X = 100,
                        Y = 100,
                        Width = 800,
                        Height = 600,
                        ShowCmd = 1
                    }
                }
            };

            // Live window has a different HWND, mutated title, mutated tabs, different bounds,
            // but bears the exact stamped 64-bit kernel property token.
            env.Windows[7001] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 7001,
                InstanceToken = testToken,
                ProcessName = "notepad",
                Title = "CompletelyDifferentNote.txt - Notepad",
                Tabs = new[] { "CompletelyDifferentNote.txt" },
                Placement = new WindowPlacement { ShowCmd = 1, NormalPosition = new WindowRect(0, 0, 500, 500) }
            };

            var restorer = new WindowRestorer(env);
            var originalOut = Console.Out;
            using var sw = new StringWriter();
            try
            {
                Console.SetOut(sw);
                restorer.Restore(profile, verbose: true);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            Assert.True(env.AppliedPlacements.ContainsKey(7001));
            Assert.Equal(100, env.AppliedPlacements[7001].NormalPosition.Left);
            Assert.Equal(800, env.AppliedPlacements[7001].NormalPosition.Width);
            Assert.Equal(600, env.AppliedPlacements[7001].NormalPosition.Height);

            string output = sw.ToString();
            Assert.Contains("Match: Pass 1 (Verified Kernel Token)", output);
        }

        [Fact]
        public void NonRegression_SingleInstanceFullTitleDrift_StillRestores_Issue2()
        {
            var env = new FakeWindowingEnvironment();

            var profile = new SessionProfile
            {
                ProfileName = "issue2",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 8888,
                        ProcessName = "obsidian",
                        WindowTitle = "Welcome - Obsidian",
                        Tabs = Array.Empty<string>(),
                        X = 200,
                        Y = 200,
                        Width = 1000,
                        Height = 700,
                        ShowCmd = 1
                    }
                }
            };

            env.Windows[8001] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 8001,
                ProcessName = "obsidian",
                Title = "Daily Note 2026-11-08 - Obsidian",
                Tabs = Array.Empty<string>(),
                Placement = new WindowPlacement { ShowCmd = 1, NormalPosition = new WindowRect(0, 0, 600, 400) }
            };

            var restorer = new WindowRestorer(env);
            restorer.Restore(profile);

            Assert.True(env.AppliedPlacements.ContainsKey(8001));
            Assert.Equal(200, env.AppliedPlacements[8001].NormalPosition.Left);
            Assert.Equal(1000, env.AppliedPlacements[8001].NormalPosition.Width);
            Assert.Equal(700, env.AppliedPlacements[8001].NormalPosition.Height);
        }

        [Fact]
        public void NonRegression_LegacyAuxiliaryFloatingWindow_IsNotMatched_Issue3()
        {
            var env = new FakeWindowingEnvironment();

            var profile = new SessionProfile
            {
                ProfileName = "issue3",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 9001,
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
                        Hwnd = 9002,
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

            // Only 1 real app window is open
            env.Windows[9001] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 9001,
                ProcessName = "max",
                Title = "Brain MAX",
                IsApp = true
            };

            var restorer = new WindowRestorer(env);
            restorer.Restore(profile);

            // Real app window is restored
            Assert.True(env.AppliedPlacements.ContainsKey(9001));
            // Target 2 (Floating Window) has no candidate and is not placed
            Assert.Single(env.AppliedPlacements);
        }

        [Fact]
        public void Stage2_DesktopAffinity_BeatsHiddenNormalPosition()
        {
            var env = new FakeWindowingEnvironment();
            Guid deskA = Guid.NewGuid();
            Guid deskB = Guid.NewGuid();

            var profile = new SessionProfile
            {
                ProfileName = "desktop-affinity-beats-geometry",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 1001,
                        ProcessName = "chrome",
                        WindowTitle = "Work Session - Chrome",
                        Tabs = new[] { "Work Session" },
                        X = 100,
                        Y = 100,
                        Width = 800,
                        Height = 600,
                        ShowCmd = 3,
                        DesktopId = deskA
                    },
                    new WindowRecord
                    {
                        TargetId = 2,
                        Hwnd = 1002,
                        ProcessName = "chrome",
                        WindowTitle = "Personal Session - Chrome",
                        Tabs = new[] { "Personal Session" },
                        X = 200,
                        Y = 200,
                        Width = 1000,
                        Height = 700,
                        ShowCmd = 3,
                        DesktopId = deskB
                    }
                }
            };

            // Live Candidate 1 resides on Desktop A but has a different hidden NormalPosition
            env.Windows[3001] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 3001,
                ProcessName = "chrome",
                Title = "News Feed - Chrome",
                Tabs = new[] { "News Feed" },
                DesktopId = deskA,
                Placement = new WindowPlacement { ShowCmd = 3, NormalPosition = new WindowRect(500, 500, 1400, 1100) }
            };

            // Live Candidate 2 resides on Desktop B but has hidden NormalPosition matching Target 1 (100, 100, 900, 700 => 800x600)
            env.Windows[3002] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 3002,
                ProcessName = "chrome",
                Title = "Music Player - Chrome",
                Tabs = new[] { "Music Player" },
                DesktopId = deskB,
                Placement = new WindowPlacement { ShowCmd = 3, NormalPosition = new WindowRect(100, 100, 900, 700) }
            };

            var matcher = new WindowMatcher(env);
            var result = matcher.Match(profile.Windows);

            // Candidate 1 matches Target 1 via Desktop Affinity (+200.0) overriding Candidate 2's matching hidden geometry (+50.0)
            Assert.Equal(3001, result.Assignments[1]);
            Assert.Equal("Pass 2 (Bipartite: Desktop Affinity)", result.MatchReasons[1]);

            // Candidate 2 matches Target 2 via Desktop Affinity (+200.0)
            Assert.Equal(3002, result.Assignments[2]);
            Assert.Equal("Pass 2 (Bipartite: Desktop Affinity)", result.MatchReasons[2]);
        }

        [Fact]
        public void Stage2_UniformMaximizedWindows_ReportsOrderFallbackTelemetry()
        {
            var env = new FakeWindowingEnvironment();

            var profile = new SessionProfile
            {
                ProfileName = "uniform-maximized",
                Windows = new[]
                {
                    new WindowRecord
                    {
                        TargetId = 1,
                        Hwnd = 1001,
                        ProcessName = "waterfox",
                        WindowTitle = "First Session - Waterfox",
                        Tabs = new[] { "First Session" },
                        X = 0,
                        Y = 0,
                        Width = 1920,
                        Height = 1080,
                        ShowCmd = 3
                    },
                    new WindowRecord
                    {
                        TargetId = 2,
                        Hwnd = 1002,
                        ProcessName = "waterfox",
                        WindowTitle = "Second Session - Waterfox",
                        Tabs = new[] { "Second Session" },
                        X = 0,
                        Y = 0,
                        Width = 1920,
                        Height = 1080,
                        ShowCmd = 3
                    }
                }
            };

            // Two live maximized windows on the same desktop with identical geometry and completely drifted titles/tabs
            env.Windows[4001] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 4001,
                ProcessName = "waterfox",
                Title = "Alpha Tab - Waterfox",
                Tabs = new[] { "Alpha Tab" },
                Placement = new WindowPlacement { ShowCmd = 3, NormalPosition = new WindowRect(0, 0, 1920, 1080) }
            };

            env.Windows[4002] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 4002,
                ProcessName = "waterfox",
                Title = "Beta Tab - Waterfox",
                Tabs = new[] { "Beta Tab" },
                Placement = new WindowPlacement { ShowCmd = 3, NormalPosition = new WindowRect(0, 0, 1920, 1080) }
            };

            var matcher = new WindowMatcher(env);
            var result = matcher.Match(profile.Windows);

            // Windows match successfully but telemetry reflects Order Fallback because geometry is non-discriminating
            Assert.Equal(4001, result.Assignments[1]);
            Assert.Equal("Pass 2 (Bipartite: Order Fallback)", result.MatchReasons[1]);

            Assert.Equal(4002, result.Assignments[2]);
            Assert.Equal("Pass 2 (Bipartite: Order Fallback)", result.MatchReasons[2]);
        }
    }
}
