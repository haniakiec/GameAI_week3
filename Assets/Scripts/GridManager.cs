using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

/// <summary>
/// Owns the grid. The grid (an array of Nodes) is the single source of truth:
/// the generator decides which cells are walls, and this class then
///   1. stores them as Nodes for A* pathfinding, and
///   2. paints them onto a Tilemap, which provides the visuals and the collision.
/// Walls are never detected with physics queries any more, so the pathfinding
/// grid and the walls on screen cannot disagree.
/// </summary>
public class GridManager : MonoBehaviour
{
    [Header("Grid size (in cells, including the outer wall ring)")]
    public Vector2Int gridSize = new Vector2Int(24, 14);

    [Header("Walls are painted onto this Tilemap")]
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private TileBase wallTile;

    [Header("Decides which cells are walls (optional)")]
    [SerializeField] private ShuffleExistingWalls wallShuffler;

    private Node[,] grid;
    private Vector3Int cellOffset; // Tilemap cell that corresponds to grid cell (0, 0)

    // Kept so old code still compiles. The Tilemap's Grid component now decides the cell size.
    public float nodeDiameter => wallTilemap.layoutGrid.cellSize.x;

    private void Awake()
    {
        // Centre the grid on this object's position.
        cellOffset = wallTilemap.WorldToCell(transform.position)
                     - new Vector3Int(gridSize.x / 2, gridSize.y / 2, 0);

        // Ask the generator which cells are walls, or just use the outer ring.
        bool[,] blocked = (wallShuffler != null)
            ? wallShuffler.Generate(this)
            : BorderOnlyMap();

        ApplyMap(blocked);
    }

    // ------------------------------------------------------------------
    // Building the map
    // ------------------------------------------------------------------

    /// <summary>A map where only the outer ring of cells is a wall. true = wall.</summary>
    public bool[,] BorderOnlyMap()
    {
        bool[,] blocked = new bool[gridSize.x, gridSize.y];
        for (int x = 0; x < gridSize.x; x++)
            for (int y = 0; y < gridSize.y; y++)
                blocked[x, y] = (x == 0 || y == 0 || x == gridSize.x - 1 || y == gridSize.y - 1);
        return blocked;
    }

    /// <summary>Turns a wall map into pathfinding Nodes and Tilemap tiles.</summary>
    private void ApplyMap(bool[,] blocked)
    {
        wallTilemap.ClearAllTiles();
        grid = new Node[gridSize.x, gridSize.y];

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                grid[x, y] = new Node(!blocked[x, y], CellToWorld(x, y), x, y);

                if (blocked[x, y])
                    wallTilemap.SetTile(CellToTilemap(x, y), wallTile);
            }
        }
    }

    // ------------------------------------------------------------------
    // Conversions between grid cells and world positions
    // ------------------------------------------------------------------

    private Vector3Int CellToTilemap(int x, int y)
    {
        return new Vector3Int(x + cellOffset.x, y + cellOffset.y, 0);
    }

    /// <summary>World position of the centre of a grid cell.</summary>
    public Vector2 CellToWorld(int x, int y)
    {
        return wallTilemap.GetCellCenterWorld(CellToTilemap(x, y));
    }

    /// <summary>Grid cell containing a world position (clamped to the grid).</summary>
    public Vector2Int WorldToCell(Vector2 worldPosition)
    {
        Vector3Int c = wallTilemap.WorldToCell(worldPosition) - cellOffset;
        return new Vector2Int(Mathf.Clamp(c.x, 0, gridSize.x - 1),
                              Mathf.Clamp(c.y, 0, gridSize.y - 1));
    }

    // ------------------------------------------------------------------
    // Used by A* pathfinding (same signatures as before)
    // ------------------------------------------------------------------

    public Node NodeFromWorldPoint(Vector2 worldPosition)
    {
        Vector2Int c = WorldToCell(worldPosition);
        return grid[c.x, c.y];
    }

    public List<Node> GetNeighbors(Node node)
    {
        List<Node> neighbors = new List<Node>();
        // Offsets for Up, Down, Left, Right
        int[] dx = { 0, 0, -1, 1 };
        int[] dy = { 1, -1, 0, 0 };

        for (int i = 0; i < 4; i++)
        {
            int checkX = node.xgrid + dx[i];
            int checkY = node.ygrid + dy[i];

            if (checkX >= 0 && checkX < gridSize.x && checkY >= 0 && checkY < gridSize.y)
            {
                Node neighbor = grid[checkX, checkY];
                if (neighbor.walkable) neighbors.Add(neighbor);
            }
        }
        return neighbors;
    }

    // Shows the grid in the Scene view (turn Gizmos on). White = walkable, red = wall.
    private void OnDrawGizmos()
    {
        if (grid == null || wallTilemap == null) return;

        foreach (Node n in grid)
        {
            Gizmos.color = n.walkable ? new Color(1f, 1f, 1f, 0.25f) : new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawCube(n.worldPosition, Vector3.one * (nodeDiameter - 0.1f));
        }
    }
}
