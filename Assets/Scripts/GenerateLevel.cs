using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Builds the level in two steps:
///   1. Generate the walls with LevelLayout.GenerateMaze (a bool[,] array, true = wall).
///      The maze is connected by construction, so no clean-up pass is needed.
///   2. Move the player, exit, hunters, collectibles... onto random free cells.
/// GridManager calls Generate() once and then draws the returned wall map.
/// </summary>
public class GenerateLevel : MonoBehaviour
{
    [System.Serializable]
    public class PlacementGroup
    {
        public string name = "Group";

        [Tooltip("Objects to place. With 'Place Children' ticked, their children are placed instead " +
                 "(e.g. one parent that holds ten collectibles).")]
        public Transform[] objects;
        public bool placeChildren;

        [Tooltip("Minimum walking distance, in cells, from the player's start cell. " +
                 "If no cell is that far away, the farthest free cell is used.")]
        public int minDistanceFromPlayer = 4;

        [Tooltip("Optional: spawn this prefab 'count' times instead of using scene objects.")]
        public GameObject prefab;
        public int count = 1;
        [Tooltip("Optional parent for the spawned copies, to keep the Hierarchy tidy.")]
        public Transform container;

        [System.NonSerialized] public List<Transform> spawned = new List<Transform>();
    }

    [Header("Maze")]
    [SerializeField] private int corridorSpacing = 3;   // 2 = thin walls, 3 = 2x2 wall blocks
    [SerializeField, Range(0f, 1f)] private float deadEndRemoval = 0.9f;
    [SerializeField, Range(0f, 1f)] private float extraLoops = 0.1f;
    [SerializeField] private bool mirrorLeftRight = false;

    [Header("Objects placed after the walls")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform exitTransform;
    [SerializeField] private int minExitDistance = 12;    // walking distance from the player, in cells
    [SerializeField] private PlacementGroup[] groups;     // hunters, rival, collectibles, pickups...

    [Header("Retries")]
    [SerializeField] private int maxGenerationRetries = 50;

    [Header("Seed")]
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int mazeSeed = 12345;

    private static readonly Vector2Int[] Directions =
        { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    // ------------------------------------------------------------------
    // Entry point (called by GridManager)
    // ------------------------------------------------------------------

    /// <summary>Generates the walls, places all objects, and returns the wall map. true = wall.</summary>
    public bool[,] Generate(GridManager grid)
    {
        if (playerTransform == null || exitTransform == null)
        {
            Debug.LogError("Missing player/exit references in GenerateLevel!");
            return grid.BorderOnlyMap();
        }

        int seed = useRandomSeed ? Random.Range(0, 999999) : mazeSeed;
        Debug.Log($"Current Maze Seed: {seed}");

        // A private generator, so we don't disturb Unity's global Random used by other scripts.
        System.Random rng = new System.Random(seed);

        SpawnGroups();

        // How many objects need a free cell: player + exit + everything in the groups.
        int needed = 2;
        foreach (PlacementGroup g in groups)
            foreach (Transform t in ObjectsOf(g)) needed++;

        for (int attempt = 1; attempt <= maxGenerationRetries; attempt++)
        {
            bool[,] blocked = LevelLayout.GenerateMaze(grid.gridSize.x, grid.gridSize.y, corridorSpacing,
                                                       deadEndRemoval, extraLoops, mirrorLeftRight, rng);
            List<Vector2Int> freeCells = FreeCells(blocked);

            // Skip layouts that leave too little room (the factor 2 keeps the arena from feeling cramped).
            if (freeCells.Count < needed * 2) continue;

            if (!TryPlaceObjects(grid, blocked, freeCells, rng)) continue;

            Physics2D.SyncTransforms(); // make the physics engine see the new positions straight away
            Debug.Log($"Valid maze generated on attempt {attempt}!");
            return blocked;
        }

        Debug.LogWarning("Could not find a valid maze after max retries. Using an empty arena.");
        bool[,] empty = grid.BorderOnlyMap();
        if (!TryPlaceObjects(grid, empty, FreeCells(empty), rng))
            Debug.LogError("The grid is too small for all the objects. Increase Grid Size.");
        Physics2D.SyncTransforms();
        return empty;
    }

    // ------------------------------------------------------------------
    // All cells that are not a wall
    // ------------------------------------------------------------------
    private List<Vector2Int> FreeCells(bool[,] blocked)
    {
        List<Vector2Int> cells = new List<Vector2Int>();

        for (int x = 0; x < blocked.GetLength(0); x++)
            for (int y = 0; y < blocked.GetLength(1); y++)
                if (!blocked[x, y]) cells.Add(new Vector2Int(x, y));

        return cells;
    }

    /// <summary>
    /// Breadth-first search. Returns the walking distance (in cells) from 'start' to every cell,
    /// or -1 for cells that are walls or cannot be reached.
    /// </summary>
    private int[,] Distances(bool[,] blocked, Vector2Int start)
    {
        int w = blocked.GetLength(0);
        int h = blocked.GetLength(1);

        int[,] dist = new int[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                dist[x, y] = -1;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        dist[start.x, start.y] = 0;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            foreach (Vector2Int d in Directions)
            {
                Vector2Int next = current + d;

                if (next.x < 0 || next.y < 0 || next.x >= w || next.y >= h) continue;
                if (blocked[next.x, next.y] || dist[next.x, next.y] != -1) continue;

                dist[next.x, next.y] = dist[current.x, current.y] + 1;
                queue.Enqueue(next);
            }
        }

        return dist;
    }

    // ------------------------------------------------------------------
    // Step 2: move the objects onto free cells
    // ------------------------------------------------------------------
    private bool TryPlaceObjects(GridManager grid, bool[,] blocked, List<Vector2Int> freeCells, System.Random rng)
    {
        // Free cells in random order. Each object takes one cell, so no two objects share a cell.
        List<Vector2Int> free = new List<Vector2Int>(freeCells);
        Shuffle(free, rng);

        if (free.Count == 0) return false;

        // Player: any free cell.
        Vector2Int playerCell = free[0];
        free.RemoveAt(0);
        MoveTo(grid, playerTransform, playerCell);

        // Walking distance from the player to every cell, used for the distance rules below.
        int[,] dist = Distances(blocked, playerCell);

        // Exit: far from the player.
        Vector2Int exitCell;
        if (!TakeCell(free, dist, minExitDistance, out exitCell)) return false;
        MoveTo(grid, exitTransform, exitCell);

        // Hunters, collectibles, pickups...
        foreach (PlacementGroup g in groups)
        {
            foreach (Transform t in ObjectsOf(g))
            {
                Vector2Int cell;
                if (!TakeCell(free, dist, g.minDistanceFromPlayer, out cell)) return false;
                MoveTo(grid, t, cell);
            }
        }

        return true;
    }

    private void SpawnGroups()
    {
        foreach (PlacementGroup g in groups)
        {
            g.spawned = new List<Transform>();
            if (g.prefab == null) continue;

            for (int i = 0; i < g.count; i++)
            {
                GameObject obj = Instantiate(g.prefab, g.prefab.transform.position,
                                             g.prefab.transform.rotation, g.container);
                obj.name = $"{g.prefab.name} {i + 1}";
                g.spawned.Add(obj.transform);
            }
        }
    }

    /// <summary>
    /// Removes and returns a free cell at least 'minDist' steps from the player.
    /// If none is that far away, takes the farthest one instead. Fails only if no cell is left.
    /// </summary>
    private bool TakeCell(List<Vector2Int> free, int[,] dist, int minDist, out Vector2Int cell)
    {
        int farthest = -1;

        for (int i = 0; i < free.Count; i++)
        {
            int d = dist[free[i].x, free[i].y];

            if (d >= minDist)
            {
                cell = free[i];
                free.RemoveAt(i);
                return true;
            }

            if (farthest == -1 || d > dist[free[farthest].x, free[farthest].y])
                farthest = i;
        }

        if (farthest == -1)
        {
            cell = default(Vector2Int);
            return false;
        }

        cell = free[farthest];
        free.RemoveAt(farthest);
        return true;
    }

    private void MoveTo(GridManager grid, Transform target, Vector2Int cell)
    {
        Vector2 p = grid.CellToWorld(cell.x, cell.y);
        target.position = new Vector3(p.x, p.y, target.position.z); // keep the original z
    }

    /// <summary>The objects of a group: the listed objects, or their children.</summary>
    private IEnumerable<Transform> ObjectsOf(PlacementGroup g)
    {
        if (g.spawned != null)
            foreach (Transform t in g.spawned)
                if (t != null) yield return t;

        if (g.objects == null) yield break;

        foreach (Transform root in g.objects)
        {
            if (root == null) continue;

            if (g.placeChildren)
            {
                foreach (Transform child in root) yield return child;
            }
            else
            {
                yield return root;
            }
        }
    }

    private void Shuffle(List<Vector2Int> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            Vector2Int temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
