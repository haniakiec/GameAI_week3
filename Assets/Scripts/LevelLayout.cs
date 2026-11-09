using System.Collections.Generic;

/// <summary>
/// Pac-Man style wall layouts: dense, with many separate paths and crossings.
/// Pure C# (no Unity types), so it only works on a bool[,] where true = wall.
///
/// Idea: lay a coarse lattice of "junctions" over the grid, spaced 'spacing' cells apart.
/// The cells between two neighbouring junctions are a "passage" that is either open or closed.
/// Everything that is neither a junction nor an open passage stays wall.
///   spacing = 2: corridors 1 cell wide, walls 1 cell thick
///   spacing = 3: corridors 1 cell wide, walls 2 cells thick (2x2 blocks, closest to Pac-Man)
///
/// Steps:
///   1. Randomized depth-first search opens a spanning tree: every junction is reachable.
///   2. Dead ends are removed by opening one more passage, which creates loops.
///   3. A few extra passages are opened at random, which creates more crossings.
///   4. Optionally only the left half is built and then mirrored (the Pac-Man look).
/// </summary>
public static class LevelLayout
{
    // Directions: 0 = right, 1 = up, 2 = left, 3 = down.
    private static readonly int[] DX = { 1, 0, -1, 0 };
    private static readonly int[] DY = { 0, 1, 0, -1 };

    /// <summary>
    /// Builds a wall map of the given size. true = wall. The outer ring is always wall.
    /// The junctions fit exactly when (width - 3) and (height - 3) are divisible by 'spacing',
    /// e.g. 25 x 15 for spacing 2, or 24 x 15 for spacing 3. Leftover rows/columns just stay wall.
    /// </summary>
    /// <param name="deadEndRemoval">0..1, chance that each dead end gets opened up into a loop.</param>
    /// <param name="extraLoops">0..1, chance that each remaining closed passage is opened.</param>
    /// <param name="mirror">true = left/right symmetric layout.</param>
    public static bool[,] GenerateMaze(int width, int height, int spacing,
                                       float deadEndRemoval, float extraLoops,
                                       bool mirror, System.Random rng)
    {
        if (spacing < 2) spacing = 2;

        // Number of junctions in each direction.
        int nx = (width - 3) / spacing + 1;
        int ny = (height - 3) / spacing + 1;

        if (nx < 2 || ny < 2) return BorderOnly(width, height); // grid too small for a maze

        Lattice lat = new Lattice(nx, ny);

        // With mirroring we only build the left half and copy it over afterwards.
        int cols = mirror ? (nx + 1) / 2 : nx;

        CarveSpanningTree(lat, cols, rng);
        RemoveDeadEnds(lat, cols, deadEndRemoval, rng);
        AddExtraLoops(lat, cols, extraLoops, rng);

        if (mirror) MirrorLeftToRight(lat, cols, rng);

        return ToWallMap(lat, width, height, spacing);
    }

    // ------------------------------------------------------------------
    // Step 1: randomized depth-first search (recursive backtracker)
    // ------------------------------------------------------------------
    private static void CarveSpanningTree(Lattice lat, int cols, System.Random rng)
    {
        bool[,] visited = new bool[lat.nx, lat.ny];
        Stack<int> stack = new Stack<int>(); // junction index = j * nx + i

        int startI = rng.Next(cols);
        int startJ = rng.Next(lat.ny);
        visited[startI, startJ] = true;
        stack.Push(startJ * lat.nx + startI);

        while (stack.Count > 0)
        {
            int n = stack.Peek();
            int i = n % lat.nx;
            int j = n / lat.nx;

            // Directions that lead to a junction we haven't visited yet.
            List<int> options = new List<int>();
            for (int d = 0; d < 4; d++)
            {
                int ni = i + DX[d];
                int nj = j + DY[d];
                if (ni >= 0 && ni < cols && nj >= 0 && nj < lat.ny && !visited[ni, nj])
                    options.Add(d);
            }

            if (options.Count == 0)
            {
                stack.Pop(); // dead end of the search: backtrack
                continue;
            }

            int dir = options[rng.Next(options.Count)];
            lat.SetOpen(i, j, dir, true);

            int ti = i + DX[dir];
            int tj = j + DY[dir];
            visited[ti, tj] = true;
            stack.Push(tj * lat.nx + ti);
        }
    }

    // ------------------------------------------------------------------
    // Step 2: turn dead ends into loops
    // ------------------------------------------------------------------
    private static void RemoveDeadEnds(Lattice lat, int cols, float chance, System.Random rng)
    {
        for (int j = 0; j < lat.ny; j++)
        {
            for (int i = 0; i < cols; i++)
            {
                if (lat.OpenCount(i, j) != 1) continue;     // not a dead end
                if (rng.NextDouble() >= chance) continue;

                List<int> closed = new List<int>();
                List<int> closedToDeadEnd = new List<int>();

                for (int d = 0; d < 4; d++)
                {
                    int ni = i + DX[d];
                    int nj = j + DY[d];
                    if (ni < 0 || ni >= cols || nj < 0 || nj >= lat.ny) continue;
                    if (lat.IsOpen(i, j, d)) continue;

                    closed.Add(d);
                    if (lat.OpenCount(ni, nj) == 1) closedToDeadEnd.Add(d);
                }

                // Prefer joining two dead ends together: it fixes both at once.
                List<int> choices = closedToDeadEnd.Count > 0 ? closedToDeadEnd : closed;
                if (choices.Count == 0) continue;

                lat.SetOpen(i, j, choices[rng.Next(choices.Count)], true);
            }
        }
    }

    // ------------------------------------------------------------------
    // Step 3: open a few more passages for extra crossings
    // ------------------------------------------------------------------
    private static void AddExtraLoops(Lattice lat, int cols, float chance, System.Random rng)
    {
        for (int j = 0; j < lat.ny; j++)
        {
            for (int i = 0; i < cols; i++)
            {
                if (i + 1 < cols && !lat.IsOpen(i, j, 0) && rng.NextDouble() < chance)
                    lat.SetOpen(i, j, 0, true);

                if (j + 1 < lat.ny && !lat.IsOpen(i, j, 1) && rng.NextDouble() < chance)
                    lat.SetOpen(i, j, 1, true);
            }
        }
    }

    // ------------------------------------------------------------------
    // Step 4: mirror the left half onto the right half
    // ------------------------------------------------------------------
    private static void MirrorLeftToRight(Lattice lat, int cols, System.Random rng)
    {
        int nx = lat.nx;

        for (int j = 0; j < lat.ny; j++)
        {
            for (int i = 0; i < cols; i++)
            {
                // Vertical passage in column i <-> the same one in column nx-1-i.
                if (lat.IsOpen(i, j, 1)) lat.SetOpen(nx - 1 - i, j, 1, true);

                // Horizontal passage i (between columns i and i+1) <-> passage nx-2-i.
                if (i + 1 < cols && lat.IsOpen(i, j, 0)) lat.SetOpen(nx - 2 - i, j, 0, true);
            }
        }

        // With an even number of columns the two halves are separate so far. Join them with
        // at least one passage across the middle, so everything stays connected.
        if (nx % 2 == 0)
        {
            int c = nx / 2 - 1; // the passage between the two middle columns
            bool any = false;

            for (int j = 0; j < lat.ny; j++)
            {
                if (rng.NextDouble() < 0.4)
                {
                    lat.SetOpen(c, j, 0, true);
                    any = true;
                }
            }

            if (!any) lat.SetOpen(c, rng.Next(lat.ny), 0, true);
        }
    }

    // ------------------------------------------------------------------
    // Convert the lattice into a wall map
    // ------------------------------------------------------------------
    private static bool[,] ToWallMap(Lattice lat, int width, int height, int spacing)
    {
        bool[,] blocked = new bool[width, height];

        // Start with everything wall, then carve out junctions and open passages.
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                blocked[x, y] = true;

        for (int j = 0; j < lat.ny; j++)
        {
            for (int i = 0; i < lat.nx; i++)
            {
                int x = 1 + i * spacing;
                int y = 1 + j * spacing;

                blocked[x, y] = false;

                if (lat.IsOpen(i, j, 0))
                    for (int k = 1; k < spacing; k++) blocked[x + k, y] = false;

                if (lat.IsOpen(i, j, 1))
                    for (int k = 1; k < spacing; k++) blocked[x, y + k] = false;
            }
        }

        return blocked;
    }

    private static bool[,] BorderOnly(int width, int height)
    {
        bool[,] blocked = new bool[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                blocked[x, y] = (x == 0 || y == 0 || x == width - 1 || y == height - 1);
        return blocked;
    }

    // ------------------------------------------------------------------
    // The lattice: which passages between neighbouring junctions are open
    // ------------------------------------------------------------------
    private class Lattice
    {
        public readonly int nx, ny;
        private readonly bool[,] right; // right[i, j]: passage between junction (i, j) and (i+1, j)
        private readonly bool[,] up;    // up[i, j]:    passage between junction (i, j) and (i, j+1)

        public Lattice(int nx, int ny)
        {
            this.nx = nx;
            this.ny = ny;
            right = new bool[nx, ny];
            up = new bool[nx, ny];
        }

        public bool IsOpen(int i, int j, int dir)
        {
            int ni = i + DX[dir];
            int nj = j + DY[dir];
            if (ni < 0 || ni >= nx || nj < 0 || nj >= ny) return false;

            switch (dir)
            {
                case 0: return right[i, j];
                case 1: return up[i, j];
                case 2: return right[i - 1, j];
                default: return up[i, j - 1];
            }
        }

        public void SetOpen(int i, int j, int dir, bool value)
        {
            switch (dir)
            {
                case 0: right[i, j] = value; break;
                case 1: up[i, j] = value; break;
                case 2: right[i - 1, j] = value; break;
                default: up[i, j - 1] = value; break;
            }
        }

        public int OpenCount(int i, int j)
        {
            int count = 0;
            for (int d = 0; d < 4; d++)
                if (IsOpen(i, j, d)) count++;
            return count;
        }
    }
}
