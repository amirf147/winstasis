using System;
using System.Collections.Generic;
using System.Linq;
using WinStasis.Interfaces;
using WinStasis.Models;

namespace WinStasis.Matching
{
    public sealed class WindowMatchResult
    {
        public Dictionary<int, long> Assignments { get; } = new(); // TargetId -> hWnd
        public Dictionary<int, string> MatchReasons { get; } = new(); // TargetId -> Telemetry string
    }

    /// <summary>
    /// Two-Stage Multi-Signal Bipartite Window Matching Pipeline (ADR-0013).
    /// Stage 1: Verified Intra-Session Fast-Path (Kernel Token, HWND fallback, unambiguous exact title).
    /// Stage 2: Per-Process Cohort Bipartite Matching (Kuhn-Munkres global optimization).
    /// </summary>
    public sealed class WindowMatcher
    {
        private static readonly string[] Delimiters = new[] { " - ", " \u2014 ", " \u2013 ", " | ", " \u00b7 " };
        private readonly IWindowingEnvironment _env;

        public WindowMatcher(IWindowingEnvironment env)
        {
            _env = env;
        }

        public WindowMatchResult Match(IReadOnlyList<WindowRecord> windowsToRestore)
        {
            var result = new WindowMatchResult();
            if (windowsToRestore == null || windowsToRestore.Count == 0)
            {
                return result;
            }

            // Candidate Qualification (ADR-0010, ADR-0011)
            var unallocatedCandidates = new List<long>();
            foreach (long hwnd in _env.GetVisibleWindows())
            {
                if (_env.IsWindowAlive(hwnd) &&
                    _env.IsWindowVisible(hwnd) &&
                    _env.IsAppWindow(hwnd) &&
                    !string.IsNullOrWhiteSpace(_env.GetWindowTitle(hwnd)))
                {
                    if (!unallocatedCandidates.Contains(hwnd))
                    {
                        unallocatedCandidates.Add(hwnd);
                    }
                }
            }

            var unallocatedRecords = new List<WindowRecord>(windowsToRestore);

            // =====================================================================
            // STAGE 1: VERIFIED INTRA-SESSION FAST-PATH
            // =====================================================================

            // 1. Kernel Token Match
            foreach (var win in unallocatedRecords.ToList())
            {
                if (win.InstanceToken == 0)
                {
                    continue;
                }

                long matchedHwnd = 0;
                foreach (long hWnd in unallocatedCandidates)
                {
                    if (_env.GetWindowProcessName(hWnd).Equals(win.ProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (_env.GetWindowInstanceToken(hWnd) == win.InstanceToken)
                        {
                            matchedHwnd = hWnd;
                            break;
                        }
                    }
                }

                if (matchedHwnd != 0)
                {
                    result.Assignments[win.TargetId] = matchedHwnd;
                    result.MatchReasons[win.TargetId] = "Pass 1 (Verified Kernel Token)";
                    unallocatedCandidates.Remove(matchedHwnd);
                    unallocatedRecords.Remove(win);
                }
            }

            // 2. Legacy Fast-Path HWND Fallback (Token == 0)
            foreach (var win in unallocatedRecords.ToList())
            {
                if (win.InstanceToken != 0 || win.Hwnd == 0)
                {
                    continue;
                }

                if (unallocatedCandidates.Contains(win.Hwnd))
                {
                    string liveProc = _env.GetWindowProcessName(win.Hwnd);
                    if (liveProc.Equals(win.ProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        string liveTitle = _env.GetWindowTitle(win.Hwnd);
                        bool exactTitle = liveTitle == win.WindowTitle;
                        bool stolen = !exactTitle && unallocatedRecords.Any(other =>
                            other != win &&
                            other.ProcessName.Equals(win.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                            other.WindowTitle == liveTitle);

                        if (exactTitle || !stolen)
                        {
                            result.Assignments[win.TargetId] = win.Hwnd;
                            result.MatchReasons[win.TargetId] = "Pass 1 (Fast-Path HWND)";
                            unallocatedCandidates.Remove(win.Hwnd);
                            unallocatedRecords.Remove(win);
                        }
                    }
                }
            }

            // 3. Exact Process + Title Match (Unambiguous)
            foreach (var win in unallocatedRecords.ToList())
            {
                bool hasAmbiguousRecord = unallocatedRecords.Any(other =>
                    other != win &&
                    other.ProcessName.Equals(win.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                    other.WindowTitle == win.WindowTitle &&
                    (other.X != win.X || other.Y != win.Y || other.Width != win.Width || other.Height != win.Height || other.ShowCmd != win.ShowCmd));

                if (hasAmbiguousRecord)
                {
                    continue;
                }

                long matchingCandidate = unallocatedCandidates.FirstOrDefault(hWnd =>
                    _env.GetWindowProcessName(hWnd).Equals(win.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                    _env.GetWindowTitle(hWnd) == win.WindowTitle);

                if (matchingCandidate != 0)
                {
                    result.Assignments[win.TargetId] = matchingCandidate;
                    result.MatchReasons[win.TargetId] = "Pass 1 (Exact Process + Title)";
                    unallocatedCandidates.Remove(matchingCandidate);
                    unallocatedRecords.Remove(win);
                }
            }

            // =====================================================================
            // STAGE 2: PER-PROCESS COHORT BIPARTITE MATCHING (KUHN-MUNKRES)
            // =====================================================================
            var processCohorts = unallocatedRecords
                .GroupBy(w => w.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var cohort in processCohorts)
            {
                string procName = cohort.Key;
                string procBase = procName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? procName[..^4]
                    : procName;

                var cohortRecords = cohort.ToList();
                var cohortCandidates = unallocatedCandidates
                    .Where(hWnd => _env.GetWindowProcessName(hWnd).Equals(procName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                int m = cohortRecords.Count;
                int k = cohortCandidates.Count;

                if (m == 0 || k == 0)
                {
                    continue;
                }

                // 1. Lazy Tab Fetch
                bool fetchTabs = (m > 1 && k > 1);

                // 2. Comparable Live Geometry Extraction (ADR-0012)
                var liveCandidates = new List<LiveCandidate>(k);
                foreach (long hWnd in cohortCandidates)
                {
                    WindowPlacement placement = _env.GetWindowPlacement(hWnd);
                    int showCmd = placement.ShowCmd;
                    WindowRect bounds;

                    if (showCmd == 1)
                    {
                        bounds = _env.GetVisibleWindowBounds(hWnd);
                    }
                    else if (showCmd == 2)
                    {
                        bounds = placement.NormalPosition;
                    }
                    else // showCmd == 3 (Maximized)
                    {
                        bounds = placement.NormalPosition;
                        WindowRect normWorkArea = _env.GetWorkAreaForRect(placement.NormalPosition);
                        WindowRect activeWorkArea = _env.GetWorkAreaForRect(_env.GetWindowBounds(hWnd));

                        if (normWorkArea.Left != activeWorkArea.Left || normWorkArea.Top != activeWorkArea.Top)
                        {
                            int width = placement.NormalPosition.Width;
                            int height = placement.NormalPosition.Height;
                            int offsetX = placement.NormalPosition.Left - normWorkArea.Left;
                            int offsetY = placement.NormalPosition.Top - normWorkArea.Top;
                            int maxLeft = Math.Max(activeWorkArea.Left, activeWorkArea.Right - width);
                            int maxTop = Math.Max(activeWorkArea.Top, activeWorkArea.Bottom - height);
                            int x = Math.Clamp(activeWorkArea.Left + offsetX, activeWorkArea.Left, maxLeft);
                            int y = Math.Clamp(activeWorkArea.Top + offsetY, activeWorkArea.Top, maxTop);
                            bounds = new WindowRect(x, y, x + width, y + height);
                        }
                    }

                    Guid desktopId = _env.GetWindowDesktopId(hWnd);
                    string title = _env.GetWindowTitle(hWnd);
                    string[] tabs = fetchTabs ? _env.GetWindowTabs(hWnd) : Array.Empty<string>();

                    liveCandidates.Add(new LiveCandidate(hWnd, title, placement, showCmd, bounds, desktopId, tabs));
                }

                // 3. Cohort Caption Decomposition (Delimiter & Boilerplate IDF)
                var savedCandidates = cohortRecords.Select(r => new SavedCandidate(r)).ToList();

                // Split segments
                var savedSplitSegs = savedCandidates.Select(s =>
                    s.Record.WindowTitle.Split(Delimiters, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ).ToList();

                var liveSplitSegs = liveCandidates.Select(l =>
                    l.Title.Split(Delimiters, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ).ToList();

                // Identify boilerplate: segment equals ProcessName / procBase, or appears in all M saved records (when M > 1)
                var boilerplateSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                boilerplateSet.Add(procName);
                boilerplateSet.Add(procBase);

                if (m > 1)
                {
                    // Find all segments present across all saved records
                    var commonToAllSaved = new HashSet<string>(savedSplitSegs[0], StringComparer.OrdinalIgnoreCase);
                    for (int i = 1; i < m; i++)
                    {
                        commonToAllSaved.IntersectWith(savedSplitSegs[i]);
                    }

                    foreach (string seg in commonToAllSaved)
                    {
                        boilerplateSet.Add(seg);
                    }
                }

                // Populate non-boilerplate segments
                for (int i = 0; i < m; i++)
                {
                    foreach (string seg in savedSplitSegs[i])
                    {
                        if (!boilerplateSet.Contains(seg) && !savedCandidates[i].NonBoilerplateSegments.Contains(seg, StringComparer.OrdinalIgnoreCase))
                        {
                            savedCandidates[i].NonBoilerplateSegments.Add(seg);
                        }
                    }
                }

                for (int j = 0; j < k; j++)
                {
                    foreach (string seg in liveSplitSegs[j])
                    {
                        if (!boilerplateSet.Contains(seg) && !liveCandidates[j].NonBoilerplateSegments.Contains(seg, StringComparer.OrdinalIgnoreCase))
                        {
                            liveCandidates[j].NonBoilerplateSegments.Add(seg);
                        }
                    }
                }

                // Segment cohort frequencies for workspace anchor detection
                var savedSegCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var sc in savedCandidates)
                {
                    foreach (string seg in sc.NonBoilerplateSegments)
                    {
                        savedSegCounts[seg] = savedSegCounts.GetValueOrDefault(seg, 0) + 1;
                    }
                }

                var liveSegCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var lc in liveCandidates)
                {
                    foreach (string seg in lc.NonBoilerplateSegments)
                    {
                        liveSegCounts[seg] = liveSegCounts.GetValueOrDefault(seg, 0) + 1;
                    }
                }

                // Effective Tab Sets
                for (int i = 0; i < m; i++)
                {
                    var sc = savedCandidates[i];
                    if (sc.Record.Tabs != null && sc.Record.Tabs.Length > 0)
                    {
                        foreach (string tab in sc.Record.Tabs)
                        {
                            string norm = NormalizeTab(tab, procName, procBase);
                            if (!string.IsNullOrEmpty(norm))
                            {
                                sc.EffectiveTabs.Add(norm);
                            }
                        }
                    }

                    // Include non-boilerplate segments of WindowTitle so legacy profiles match against live background tabs
                    foreach (string seg in sc.NonBoilerplateSegments)
                    {
                        sc.EffectiveTabs.Add(seg);
                    }
                }

                for (int j = 0; j < k; j++)
                {
                    var lc = liveCandidates[j];
                    if (lc.Tabs != null && lc.Tabs.Length > 0)
                    {
                        foreach (string tab in lc.Tabs)
                        {
                            string norm = NormalizeTab(tab, procName, procBase);
                            if (!string.IsNullOrEmpty(norm))
                            {
                                lc.EffectiveTabs.Add(norm);
                            }
                        }
                    }

                    // Include non-boilerplate segments of live title
                    foreach (string seg in lc.NonBoilerplateSegments)
                    {
                        lc.EffectiveTabs.Add(seg);
                    }
                }

                // 4. Score Matrix Construction S[i, j]
                double[,] scores = new double[m, k];
                string[,] reasonMatrix = new string[m, k];

                for (int i = 0; i < m; i++)
                {
                    var r = savedCandidates[i];

                    // Precompute geometry scores across candidates for row i to evaluate variance
                    double[] geomScores = new double[k];
                    for (int j = 0; j < k; j++)
                    {
                        var w = liveCandidates[j];
                        double showCmd = (r.Record.ShowCmd == w.ShowCmd) ? 10.0 : 0.0;
                        double sizeDiff = Math.Abs(r.Record.Width - w.Bounds.Width) + Math.Abs(r.Record.Height - w.Bounds.Height);
                        double sizeSim = Math.Max(0.0, 1.0 - sizeDiff / 1000.0) * 25.0;
                        double posDiff = Math.Abs(r.Record.X - w.Bounds.Left) + Math.Abs(r.Record.Y - w.Bounds.Top);
                        double posSim = Math.Max(0.0, 1.0 - posDiff / 2000.0) * 15.0;
                        geomScores[j] = showCmd + sizeSim + posSim;
                    }

                    double geomMean = geomScores.Average();
                    double geomVariance = 0.0;
                    for (int j = 0; j < k; j++)
                    {
                        double diff = geomScores[j] - geomMean;
                        geomVariance += diff * diff;
                    }
                    geomVariance /= k;
                    bool geomDistinguishes = (k > 1) && (geomVariance >= 1e-4);

                    for (int j = 0; j < k; j++)
                    {
                        var w = liveCandidates[j];

                        // Tier 1: Exact Caption Match (+100000.0)
                        double titleScore = string.Equals(r.Record.WindowTitle, w.Title, StringComparison.Ordinal)
                            ? 100000.0
                            : 0.0;

                        // Tier 2: Unique Cohort Workspace Anchor (+10000.0)
                        // Tier 3: Caption Overlap (0.0 to +500.0)
                        double anchorScore = 0.0;
                        bool isUniqueAnchor = false;

                        var commonSegs = r.NonBoilerplateSegments
                            .Where(seg => w.NonBoilerplateSegments.Contains(seg, StringComparer.OrdinalIgnoreCase))
                            .ToList();

                        foreach (string seg in commonSegs)
                        {
                            if (savedSegCounts.GetValueOrDefault(seg, 0) == 1 &&
                                liveSegCounts.GetValueOrDefault(seg, 0) == 1)
                            {
                                isUniqueAnchor = true;
                                break;
                            }
                        }

                        if (isUniqueAnchor)
                        {
                            anchorScore = 10000.0;
                        }
                        else
                        {
                            int unionCount = r.NonBoilerplateSegments
                                .Union(w.NonBoilerplateSegments, StringComparer.OrdinalIgnoreCase)
                                .Count();

                            if (unionCount > 0)
                            {
                                double jaccard = (double)commonSegs.Count / unionCount;
                                anchorScore = jaccard * 500.0;
                            }
                        }

                        // Tier 3: Background Tab Overlap (0.0 to +1000.0)
                        double tabScore = 0.0;
                        if (r.EffectiveTabs.Count > 0 && w.EffectiveTabs.Count > 0)
                        {
                            int commonTabs = r.EffectiveTabs
                                .Intersect(w.EffectiveTabs, StringComparer.OrdinalIgnoreCase)
                                .Count();

                            if (commonTabs > 0)
                            {
                                double overlap = (double)commonTabs / Math.Min(r.EffectiveTabs.Count, w.EffectiveTabs.Count);
                                double jaccardTabs = (double)commonTabs / r.EffectiveTabs
                                    .Union(w.EffectiveTabs, StringComparer.OrdinalIgnoreCase)
                                    .Count();

                                tabScore = (0.6 * overlap + 0.4 * jaccardTabs) * 1000.0;
                            }
                        }

                        // Tier 4: Virtual Desktop Residence (+200.0)
                        double desktopScore = (r.Record.DesktopId != Guid.Empty && r.Record.DesktopId == w.DesktopId) ? 200.0 : 0.0;

                        // Tier 5: Contextual Geometry Similarity (0.0 to +50.0)
                        double geomScore = geomScores[j];

                        // Tier 6: Relative Z-Order Alignment (0.0 to +5.0)
                        double orderSim = 5.0 * (1.0 - Math.Abs((double)i / Math.Max(1, m) - (double)j / Math.Max(1, k)));

                        double totalScore = titleScore + anchorScore + tabScore + desktopScore + geomScore + orderSim;
                        scores[i, j] = Math.Max(0.0, totalScore);

                        // Dominant reason assignment (ADR-0013)
                        if (titleScore >= 100000.0)
                        {
                            reasonMatrix[i, j] = "Pass 2 (Bipartite: Exact Title)";
                        }
                        else if (isUniqueAnchor)
                        {
                            reasonMatrix[i, j] = "Pass 2 (Bipartite: Workspace Anchor)";
                        }
                        else if (tabScore > 0.0 && tabScore >= anchorScore)
                        {
                            reasonMatrix[i, j] = "Pass 2 (Bipartite: Tab Overlap)";
                        }
                        else if (anchorScore > 0.0)
                        {
                            reasonMatrix[i, j] = "Pass 2 (Bipartite: Caption Overlap)";
                        }
                        else if (desktopScore > 0.0)
                        {
                            reasonMatrix[i, j] = "Pass 2 (Bipartite: Desktop Affinity)";
                        }
                        else
                        {
                            if (geomScore > 0.0 && geomDistinguishes)
                            {
                                reasonMatrix[i, j] = "Pass 2 (Bipartite: Geometry)";
                            }
                            else
                            {
                                reasonMatrix[i, j] = "Pass 2 (Bipartite: Order Fallback)";
                            }
                        }
                    }
                }

                // 5. Solve via Kuhn-Munkres
                int[] assignments = HungarianSolver.SolveMaxWeight(scores);

                for (int i = 0; i < m; i++)
                {
                    int col = assignments[i];
                    if (col >= 0 && col < k)
                    {
                        var matchedCandidate = liveCandidates[col];
                        result.Assignments[savedCandidates[i].Record.TargetId] = matchedCandidate.Hwnd;
                        result.MatchReasons[savedCandidates[i].Record.TargetId] = reasonMatrix[i, col];
                        unallocatedCandidates.Remove(matchedCandidate.Hwnd);
                    }
                }
            }

            return result;
        }

        private static string NormalizeTab(string tab, string procName, string procBase)
        {
            string t = tab.Trim();
            foreach (string delim in Delimiters)
            {
                string suffix1 = delim + procName;
                if (t.EndsWith(suffix1, StringComparison.OrdinalIgnoreCase))
                {
                    t = t[..^suffix1.Length].Trim();
                }

                string suffix2 = delim + procBase;
                if (t.EndsWith(suffix2, StringComparison.OrdinalIgnoreCase))
                {
                    t = t[..^suffix2.Length].Trim();
                }
            }
            return t;
        }

        private sealed class LiveCandidate
        {
            public long Hwnd { get; }
            public string Title { get; }
            public WindowPlacement Placement { get; }
            public int ShowCmd { get; }
            public WindowRect Bounds { get; }
            public Guid DesktopId { get; }
            public string[] Tabs { get; }
            public List<string> NonBoilerplateSegments { get; } = new();
            public HashSet<string> EffectiveTabs { get; } = new(StringComparer.OrdinalIgnoreCase);

            public LiveCandidate(long hwnd, string title, WindowPlacement placement, int showCmd, WindowRect bounds, Guid desktopId, string[] tabs)
            {
                Hwnd = hwnd;
                Title = title;
                Placement = placement;
                ShowCmd = showCmd;
                Bounds = bounds;
                DesktopId = desktopId;
                Tabs = tabs;
            }
        }

        private sealed class SavedCandidate
        {
            public WindowRecord Record { get; }
            public List<string> NonBoilerplateSegments { get; } = new();
            public HashSet<string> EffectiveTabs { get; } = new(StringComparer.OrdinalIgnoreCase);

            public SavedCandidate(WindowRecord record)
            {
                Record = record;
            }
        }
    }
}

