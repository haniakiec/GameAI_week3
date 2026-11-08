using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Decides which grid cells are walls. It works only on a bool[,] array
/// (true = wall), so every wall covers exactly one or several whole cells.
/// GridManager calls Generate() once and then draws the result.
/// </summary>
public class ShuffleExistingWalls : MonoBehaviour
{
    [Header("Walls")]
    [SerializeField] private int desiredWallCount = 12;   // number of wall segments to place
    [SerializeField] private int maxSegmentLength = 4;    // each segment is 1..N cells long

    [Header("Cells that must stay free of walls")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform exitTransform;
    [SerializeField] private int safeRadiusCells = 2;     // no walls this many cells around the exit
    // Drag hunters, collectibles etc. here. Each object AND all its children keep their cell free.
    // (A parent object also reserves the cell at its own position, which is harmless.)
    [SerializeField] private Transform[] keepClear;

    [Header("Path validation")]
    [SerializeField] private int maxGenerationRetries = 50;

    [Header("Seed")]
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int mazeSeed = 12345;

    /// <summary>Builds a wall map where the player can reach the exit. true = wall.</summary>
    public bool[,] Generate(GridManager grid)
    {
        if (playerTransform == null || exitTransform == null)
        {
            Debug.LogError("Missing player/exit references in ShuffleExistingWalls!");
            return grid.BorderOnlyMap();
        }

        int seed = useRandomSeed ? Random.Range(0, 999999) : mazeSeed;
        Debug.Log($"Current Maze Seed: {seed}");

        // A private generator, so we don't disturb Unity's global Random used by other scripts.
        System.Random rng = new System.Random(seed);

        Vector2Int start = grid.WorldToCell(playerTransform.position);
        Vector2Int exit = grid.WorldToCell(exitTransform.position);
        bool[,] reserved = BuildReservedMap(grid, start, exit);

        for (int attempt = 1; attempt <= maxGenerationRetries; attempt++)
        {
            bool[,] blocked = PlaceWalls(grid, reserved, rng);

            if (IsReachable(blocked, start, exit))
            {
                Debug.Log($"Valid maze generated on attempt {attempt}!");
                return blocked;
            }
        }

        Debug.LogWarning("Could not find a valid maze after max retries. Using an empty arena.");
        return grid.BorderOnlyMap();
    }

    // ------------------------------------------------------------------
    // Cells that must never become walls
    // ------------------------------------------------------------------
    private bool[,] BuildReservedMap(GridManager grid, Vector2Int start, Vector2Int exit)
    {
        int w = grid.gridSize.x;
        int h = grid.gridSize.y;
        bool[,] reserved = new bool[w, h];

        // A square of cells around the exit.
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (Mathf.Max(Mathf.Abs(x - exit.x), Mathf.Abs(y - exit.y)) <= safeRadiusCells)
                    reserved[x, y] = true;

        reserved[start.x, start.y] = true;

        // Everything the designer listed (and all their children).
        foreach (Transform root in keepClear)
        {
            if (root == null) continue;
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
                Reserve(grid, reserved, t.position);
        }

        return reserved;
    }

    private void Reserve(GridManager grid, bool[,] reserved, Vector2 worldPosition)
    {
        Vector2Int c = grid.WorldToCell(worldPosition);
        reserved[c.x, c.y] = true;
    }

    // ------------------------------------------------------------------
    // Placing walls: straight segments of whole cells
    // ------------------------------------------------------------------
    private bool[,] PlaceWalls(GridManager grid, bool[,] reserved, System.Random rng)
    {
        int w = grid.gridSize.x;
        int h = grid.gridSize.y;
        bool[,] blocked = grid.BorderOnlyMap(); // outer ring is always wall

        int placed = 0;
        int tries = 0;

        while (placed < desiredWallCount && tries < desiredWallCount * 20)
        {
            tries++;

            int length = rng.Next(1, maxSegmentLength + 1);
            bool horizontal = rng.Next(2) == 0;
            int startX = rng.Next(1, w - 1); // interior cells only
            int startY = rng.Next(1, h - 1);

            // Collect the cells this segment would cover. Reject it if any cell is
            // outside the interior, already a wall, or reserved.
            List<Vector2Int> cells = new List<Vector2Int>();
            bool fits = true;

            for (int i = 0; i < length && fits; i++)
            {
                int x = startX + (horizontal ? i : 0);
                int y = startY + (horizontal ? 0 : i);

                if (x >= w - 1 || y >= h - 1 || blocked[x, y] || reserved[x, y])
                    fits = false;
                else
                    cells.Add(new Vector2Int(x, y));
            }

            if (!fits) continue;

            foreach (Vector2Int c in cells) blocked[c.x, c.y] = true;
            placed++;
        }

        return blocked;
    }

    // ------------------------------------------------------------------
    // Flood fill: can the player walk from 'from' to 'to'?
    // ------------------------------------------------------------------
    private bool IsReachable(bool[,] blocked, Vector2Int from, Vector2Int to)
    {
        int w = blocked.GetLength(0);
        int h = blocked.GetLength(1);

        if (blocked[from.x, from.y] || blocked[to.x, to.y]) return false;

        bool[,] visited = new bool[w, h];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        queue.Enqueue(from);
        visited[from.x, from.y] = true;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (current == to) return true;

            foreach (Vector2Int d in directions)
            {
                Vector2Int next = current + d;

                if (next.x < 0 || next.y < 0 || next.x >= w || next.y >= h) continue;
                if (blocked[next.x, next.y] || visited[next.x, next.y]) continue;

                visited[next.x, next.y] = true;
                queue.Enqueue(next);
            }
        }

        return false; // the exit is walled off
    }
}
