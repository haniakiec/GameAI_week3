using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private LayerMask obstacleLayer; 
    [SerializeField] public Vector2 gridWorldSize;   
    [SerializeField] public float nodeRadius = 0.5f;   // Size of each grid node 
    [Header("References")]
    [SerializeField] private ShuffleExistingWalls wallShuffler; 

    private Node[,] grid;
    public float nodeDiameter;
    private int gridSizeX, gridSizeY;

    private void Awake()
    {
        if (wallShuffler != null)
        {        
            //shuffle walls
            wallShuffler.GenerateAndSpawnWalls();
        }
        else
        {
            // build the grid straight away
            BuildGrid();
        }

        nodeDiameter = nodeRadius * 2f;
        //number of squares of grid
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);
    }

    private void CreateGrid()
    {
        grid = new Node[gridSizeX, gridSizeY];
        //start position
        Vector2 worldBottomLeft = (Vector2)transform.position - Vector2.right * (gridWorldSize.x / 2f) - Vector2.up * (gridWorldSize.y / 2f);

        int walkableCount = 0;
        int obstacleCount = 0;

        //go through each square in grid
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                //calculate at which point in the grid we are now in the loop
                Vector2 worldPoint = worldBottomLeft + Vector2.right * (x * nodeDiameter + nodeRadius) + Vector2.up * (y * nodeDiameter + nodeRadius);
                //check if in this point there is a physical object
                bool walkable = !Physics2D.OverlapCircle(worldPoint, nodeRadius * 0.9f, obstacleLayer);

                // Count them for our verification log
                if (walkable) walkableCount++;
                else obstacleCount++;
                //map this to our grid
                grid[x, y] = new Node(walkable, worldPoint, x, y);
            }
        }
      
        Debug.Log($"<color=cyan>Grid Successfully Generated!</color> Total Nodes: {gridSizeX * gridSizeY} | Walkable Spaces: {walkableCount} | Blocked Walls: {obstacleCount}");
    
    }

    //helper method for A* to find which node a position belongs to
    public Node NodeFromWorldPoint(Vector2 worldPosition)
    {
        float percentX = (worldPosition.x - ((Vector2)transform.position).x + gridWorldSize.x / 2f) / gridWorldSize.x;
        float percentY = (worldPosition.y - ((Vector2)transform.position).y + gridWorldSize.y / 2f) / gridWorldSize.y;
        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);

        int x = Mathf.RoundToInt((gridSizeX - 1) * percentX);
        int y = Mathf.RoundToInt((gridSizeY - 1) * percentY);
        return grid[x, y];
    }

    //helper method to get neighboring nodes for A* pathfinding
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

            // check if it's within the grid boundaries
            if (checkX >= 0 && checkX < gridSizeX && checkY >= 0 && checkY < gridSizeY)
            {
                Node neighbor = grid[checkX, checkY];

                // check if the cell is walkable
                if (neighbor.walkable)
                {
                    neighbors.Add(neighbor);
                }
            }
        }
        return neighbors;
    }

    //visually showing the gird - visible with gizmos on
    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, gridWorldSize.y, 1));

        if (grid != null)
        {
            foreach (Node n in grid)
            {
                Gizmos.color = n.walkable ? Color.white : Color.red;
                Gizmos.DrawCube(n.worldPosition, Vector3.one * (nodeDiameter - 0.1f));
            }
        }
    }

    public void BuildGrid()
    {
        nodeDiameter = nodeRadius * 2f;
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);
        CreateGrid();
    }   
}