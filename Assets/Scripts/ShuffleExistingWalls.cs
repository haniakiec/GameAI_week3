using UnityEngine;
using System.Collections.Generic;
using Vector2 = UnityEngine.Vector2;


public class ShuffleExistingWalls : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject wallPrefab;     
    [SerializeField] private Transform wallsContainer;  
    [SerializeField] private GridManager gridManager;   

    [Header("Spawn Settings")]
    [SerializeField] private Vector2Int gridDimensions;
    [SerializeField] private int desiredWallCount;
    
    [Header("Safe Zone Settings")]
    [SerializeField] private Transform exitTransform; //so walls dont block the exit
    //radius from the exit transform so it is not blocked
    [SerializeField] private float safeRadius = 2.5f;
    
    //to check if player can find a path in the seed to the exit
    [Header("Path Validation Settings")]
    [SerializeField] private Transform playerTransform;
    //how many times to retry generating a maze if the exit is in an illegal place
    [SerializeField] private int maxGenerationRetries = 50;

    [Header("Seed Settings")]
    //check if you want a random seed each time
    [SerializeField] private bool useRandomSeed = true;
    //if you want a specific seed
    [SerializeField] private int mazeSeed = 12345;

    [Header("Collision Avoidance")]
    //layers on top of which walls should not appear on
    [SerializeField] private LayerMask avoidanceLayerMask;

    private List<int> blacklistedSeeds = new List<int>();
    private List<GameObject> spawnedWalls = new List<GameObject>();

    private void Awake()
    {
        GenerateAndSpawnWalls();
    }

    public void GenerateAndSpawnWalls()
    {
        // set seed or randomize seed
        int currentSeed = mazeSeed;
        blacklistedSeeds.Add(21472);
        if (useRandomSeed)
        {
            // Keep rolling a new random seed if the current one is in our blacklist
            int safetyCounter = 0; // Prevents an infinite loop just in case
            do
            {
                currentSeed = Random.Range(0, 999999);
                safetyCounter++;
                if (safetyCounter > 100) break; // Emergency break
            }
            while (blacklistedSeeds.Contains(currentSeed));
        }

        Random.InitState(currentSeed);
        Debug.Log($"Current Maze Seed: {currentSeed}");
        

        if (wallPrefab == null || gridManager == null || playerTransform == null || exitTransform == null)
        {
            Debug.LogError("Missing references in ShuffleExistingWalls!");
            return;
        }

        int attempts = 0;
        bool validPathFound = false;

        //generate a map until there is a path to exit
        while (!validPathFound && attempts < maxGenerationRetries)
        {
            attempts++;
            //destroy walls
            if (wallsContainer != null)
            {
                for (int i = wallsContainer.childCount - 1; i >= 0; i--)
                {
                    Transform child = wallsContainer.GetChild(i);

                    // Skip deletion if this is our template object
                    if (child.name == "Obstacle A") continue;

                    DestroyImmediate(child.gameObject);
                }
            }
            spawnedWalls.Clear();

            //get ONLY the interior positions for shuffling 
            List<Vector2> interiorSpots = GetInteriorGridPositionPool();

            //shuffle interior positions using Fisher-Yates
            for (int i = 0; i < interiorSpots.Count; i++)
            {
                int randomIndex = Random.Range(i, interiorSpots.Count);
                Vector2 temp = interiorSpots[i];
                interiorSpots[i] = interiorSpots[randomIndex];
                interiorSpots[randomIndex] = temp;
            }

            /// spawn walls 
            int wallsToSpawn = desiredWallCount;
            int spawnedCount = 0;

            for (int i = 0; i < interiorSpots.Count && spawnedCount < wallsToSpawn; i++)
            {
                Vector2 spawnPos = interiorSpots[i];

                // chekc if walls are not overlapping - check if any collider in the avoidanceLayerMask is at this spot
                Collider2D hitCollider = Physics2D.OverlapBox(spawnPos, Vector2.one * 0.4f, 0f, avoidanceLayerMask);
                if (hitCollider != null)
                {
                    //if there is already wall item there do not place it
                    continue;
                }
                //so all the walls are not facing the same way
                float randomAngle = Random.Range(0, 4) * 90f;
                Quaternion spawnRot = Quaternion.Euler(0, 0, randomAngle);
                //create new wall objects
                GameObject newWall = Instantiate(wallPrefab, spawnPos, spawnRot, wallsContainer);
                newWall.layer = LayerMask.NameToLayer("Walls");
                spawnedWalls.Add(newWall);

                spawnedCount++;
            }

            // build the grid based on the created walls
            gridManager.BuildGrid();

            //check if player can get to the exit to confirm the maze layout is legal
            if (IsPathPossible(playerTransform.position, exitTransform.position))
            {
                validPathFound = true;
                Debug.Log($"Valid maze generated successfully on attempt {attempts}!");
            }
        }

        if (!validPathFound)
        {
            Debug.LogWarning("Could not find a valid path after max retries.");
        }
    }
    //creates a map of the maze with all the cells where potential walls can be placed
    private List<Vector2> GetInteriorGridPositionPool()
    {
        List<Vector2> interiorPositions = new List<Vector2>(); //positions in the maze where there could be walls placed
        float cellSize = gridManager.nodeDiameter;
        Vector2 startPos = (Vector2)transform.position - new Vector2(gridDimensions.x * cellSize / 2f, gridDimensions.y * cellSize / 2f); //player starting point

        //checking every square in the grid
        for (int x = 0; x < gridDimensions.x; x++)
        {
            for (int y = 0; y < gridDimensions.y; y++)
            {
                bool isBorder = (x == 0 || x == gridDimensions.x - 1 || y == 0 || y == gridDimensions.y - 1);
                Vector2 slotPosition = startPos + new Vector2(x * cellSize + cellSize / 2f, y * cellSize + cellSize / 2f);

                if (isBorder) continue; //not creating grid on the borders

                //create a safe radius around the exit - not to put walls there
                if (exitTransform != null)
                {
                    float distanceToExit = Vector2.Distance(slotPosition, exitTransform.position);
                    if (distanceToExit < safeRadius)
                    {
                        continue; 
                    }
                }

                interiorPositions.Add(slotPosition);
            }
        }

        return interiorPositions;
    }
    //checks if in the current seed you can get to the exit
    private bool IsPathPossible(Vector2 startPos, Vector2 targetPos)
    {
        //get the start and target of the path - player and exit
        Node startNode = gridManager.NodeFromWorldPoint(startPos);
        Node targetNode = gridManager.NodeFromWorldPoint(targetPos);

        if (startNode == null || targetNode == null || !startNode.walkable || !targetNode.walkable)
        {
            return false;
        }

        //breadth first search to check connectivity
        Queue<Node> queue = new Queue<Node>();
        HashSet<Node> visited = new HashSet<Node>();

        queue.Enqueue(startNode);
        visited.Add(startNode);

        while (queue.Count > 0)
        {
            Node currentNode = queue.Dequeue();

            if (currentNode == targetNode)
            {
                return true; // Path found!
            }

            foreach (Node neighbor in gridManager.GetNeighbors(currentNode))
            {
                if (neighbor.walkable && !visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        return false; // Target is completely walled off
    }
}