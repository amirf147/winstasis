using System;
using System.Linq;
using WinStasis;
using WinStasis.Models;
using Xunit;

namespace WinStasis.Tests
{
    public class WindowCapturerTests
    {
        [Fact]
        public void Capture_NormalSnappedWindow_CapturesVisibleDwmBounds()
        {
            var env = new FakeWindowingEnvironment();
            env.Windows[100] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 100,
                Title = "Code Editor",
                ProcessName = "Code",
                Placement = new WindowPlacement
                {
                    ShowCmd = 1,
                    NormalPosition = new WindowRect(-7, 0, 967, 1047)
                },
                VisibleBounds = new WindowRect(0, 0, 960, 1040),
                WindowBounds = new WindowRect(-7, 0, 967, 1047)
            };

            var capturer = new WindowCapturer(env);
            var profile = capturer.Capture("test", verbose: false);

            Assert.Single(profile.Windows);
            var win = profile.Windows[0];
            Assert.Equal(1, win.TargetId);
            Assert.Equal(0, win.X);
            Assert.Equal(0, win.Y);
            Assert.Equal(960, win.Width);
            Assert.Equal(1040, win.Height);
            Assert.Equal(1, win.ShowCmd);
        }

        [Fact]
        public void Capture_MinimizedWindow_PreservesNormalPosition()
        {
            var env = new FakeWindowingEnvironment();
            env.Windows[200] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 200,
                Title = "Browser",
                ProcessName = "chrome",
                Placement = new WindowPlacement
                {
                    ShowCmd = 2, // Minimized
                    NormalPosition = new WindowRect(150, 150, 1150, 850)
                },
                WindowBounds = new WindowRect(-32000, -32000, -31840, -31970)
            };

            var capturer = new WindowCapturer(env);
            var profile = capturer.Capture("test", verbose: false);

            Assert.Single(profile.Windows);
            var win = profile.Windows[0];
            Assert.Equal(150, win.X);
            Assert.Equal(150, win.Y);
            Assert.Equal(1000, win.Width);
            Assert.Equal(700, win.Height);
            Assert.Equal(2, win.ShowCmd);
        }

        [Fact]
        public void Capture_MaximizedWindow_SameMonitor_PreservesNormalPosition()
        {
            var env = new FakeWindowingEnvironment();
            env.WorkArea = new WindowRect(0, 0, 1920, 1080);
            env.Windows[300] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 300,
                Title = "Terminal",
                ProcessName = "WindowsTerminal",
                Placement = new WindowPlacement
                {
                    ShowCmd = 3, // Maximized
                    NormalPosition = new WindowRect(200, 200, 1200, 800)
                },
                WindowBounds = new WindowRect(-8, -8, 1928, 1088)
            };

            var capturer = new WindowCapturer(env);
            var profile = capturer.Capture("test", verbose: false);

            Assert.Single(profile.Windows);
            var win = profile.Windows[0];
            Assert.Equal(200, win.X);
            Assert.Equal(200, win.Y);
            Assert.Equal(1000, win.Width);
            Assert.Equal(600, win.Height);
            Assert.Equal(3, win.ShowCmd);
        }

        [Fact]
        public void Capture_MaximizedWindow_DifferentMonitor_TranslatesNormalPosition()
        {
            var env = new FakeWindowingEnvironment();
            var mon1Work = new WindowRect(0, 0, 1920, 1080);
            var mon2Work = new WindowRect(1920, 0, 3840, 1080);

            // Active monitor for window is Mon 1, but normal position was on Mon 2
            env.WorkAreaLookup = rect =>
            {
                if (rect.Left >= 1920) return mon2Work;
                return mon1Work;
            };

            env.Windows[400] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 400,
                Title = "Discord",
                ProcessName = "Discord",
                Placement = new WindowPlacement
                {
                    ShowCmd = 3, // Maximized
                    NormalPosition = new WindowRect(2020, 100, 2820, 700) // On Mon 2, 800x600, offset (100, 100)
                },
                WindowBounds = new WindowRect(-8, -8, 1928, 1088) // Maximized on Mon 1
            };

            var capturer = new WindowCapturer(env);
            var profile = capturer.Capture("test", verbose: false);

            Assert.Single(profile.Windows);
            var win = profile.Windows[0];
            Assert.Equal(3, win.ShowCmd);
            Assert.Equal(800, win.Width);
            Assert.Equal(600, win.Height);
            // Translated offset from Mon 2 (100, 100) to Mon 1 (0 + 100, 0 + 100)
            Assert.Equal(100, win.X);
            Assert.Equal(100, win.Y);
        }

        [Fact]
        public void Capture_InvalidDimensionsOrEmptyTitle_Skipped()
        {
            var env = new FakeWindowingEnvironment();
            env.Windows[501] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 501,
                Title = "   ", // Whitespace title
                ProcessName = "EmptyApp",
                Placement = new WindowPlacement
                {
                    ShowCmd = 1,
                    NormalPosition = new WindowRect(0, 0, 800, 600)
                }
            };
            env.Windows[502] = new FakeWindowingEnvironment.WindowState
            {
                Hwnd = 502,
                Title = "Zero Size App",
                ProcessName = "ZeroApp",
                Placement = new WindowPlacement
                {
                    ShowCmd = 1,
                    NormalPosition = new WindowRect(100, 100, 100, 100)
                },
                VisibleBounds = new WindowRect(100, 100, 100, 100) // Width = 0, Height = 0
            };

            var capturer = new WindowCapturer(env);
            var profile = capturer.Capture("test", verbose: true);

            Assert.Empty(profile.Windows);
        }
    }
}
