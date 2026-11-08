using System;

namespace WinStasis.Matching
{
    /// <summary>
    /// Kuhn-Munkres (Hungarian) maximum-weight bipartite matching solver.
    /// Operates in O(N^3) time on an M x K weight matrix.
    /// </summary>
    public static class HungarianSolver
    {
        public static int[] SolveMaxWeight(double[,] weights)
        {
            int m = weights.GetLength(0);
            int k = weights.GetLength(1);

            if (m == 0)
            {
                return Array.Empty<int>();
            }

            if (k == 0)
            {
                int[] emptyAssignments = new int[m];
                Array.Fill(emptyAssignments, -1);
                return emptyAssignments;
            }

            int n = Math.Max(m, k);

            double maxWeight = 0.0;
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < k; j++)
                {
                    if (weights[i, j] > maxWeight)
                    {
                        maxWeight = weights[i, j];
                    }
                }
            }

            // Construct 1-indexed square cost matrix of size (N+1) x (N+1).
            // Dummy entries receive weight -1.0 so real edges (weight >= 0.0) are strictly preferred.
            double[,] cost = new double[n + 1, n + 1];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    double w = (i < m && j < k) ? weights[i, j] : -1.0;
                    cost[i + 1, j + 1] = maxWeight - w;
                }
            }

            double[] u = new double[n + 1];
            double[] v = new double[n + 1];
            int[] p = new int[n + 1];
            int[] way = new int[n + 1];

            for (int i = 1; i <= n; ++i)
            {
                p[0] = i;
                int j0 = 0;
                double[] minv = new double[n + 1];
                Array.Fill(minv, double.PositiveInfinity);
                bool[] used = new bool[n + 1];

                do
                {
                    used[j0] = true;
                    int i0 = p[j0];
                    double delta = double.PositiveInfinity;
                    int j1 = 0;

                    for (int j = 1; j <= n; ++j)
                    {
                        if (!used[j])
                        {
                            double cur = cost[i0, j] - u[i0] - v[j];
                            if (cur < minv[j])
                            {
                                minv[j] = cur;
                                way[j] = j0;
                            }
                            if (minv[j] < delta)
                            {
                                delta = minv[j];
                                j1 = j;
                            }
                        }
                    }

                    for (int j = 0; j <= n; ++j)
                    {
                        if (used[j])
                        {
                            u[p[j]] += delta;
                            v[j] -= delta;
                        }
                        else
                        {
                            minv[j] -= delta;
                        }
                    }

                    j0 = j1;
                } while (p[j0] != 0);

                do
                {
                    int j1 = way[j0];
                    p[j0] = p[j1];
                    j0 = j1;
                } while (j0 != 0);
            }

            int[] matchCol = new int[n + 1];
            for (int j = 1; j <= n; ++j)
            {
                if (p[j] >= 1 && p[j] <= n)
                {
                    matchCol[p[j]] = j;
                }
            }

            int[] result = new int[m];
            for (int i = 0; i < m; i++)
            {
                int col = matchCol[i + 1] - 1;
                result[i] = (col >= 0 && col < k) ? col : -1;
            }

            return result;
        }
    }
}

