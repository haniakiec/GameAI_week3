using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class HunterAI : MonoBehaviour
{
    [SerializeField] private float maxSpeed = 4f;
    [SerializeField] private Transform player;
    [SerializeField] private float maxSteeringForce = 8f;
    [SerializeField] private float detectionRadius = 10f;
    [SerializeField] private float slowingRadius = 2f;
    [SerializeField] private float wanderDirectionInterval = 2f;

    //for predicting the players movement
    [Header("Target Settings")]
    private Rigidbody2D rb;
    [SerializeField] Rigidbody2D playerRb;
    private Vector2 velocity;
    private Vector2 wanderDirection;
    private float nextWanderDirectionTime;
    [Header("Feeler / Avoidance Settings")]
    [SerializeField] private float feelerLength = 3f;
    [SerializeField] private LayerMask obstacleLayer; // Assign your Wall layer in the Inspector

    [Header("Surround & Flanking Settings")]
    [SerializeField] private int hunterIndex = 0;      // Set this to 0 for Hunter 1, and 1 for Hunter 2 in the Inspector!
    [SerializeField] private int totalHunters = 2;     
    [SerializeField] private float orbitDistance = 2.5f; 
    [SerializeField] private float separationRadius = 1.5f;
    [SerializeField] private float separationWeight = 2f;
    // Recalculate the route periodically so the hunter can respond to player movement.
    [SerializeField] private float pathRefreshInterval = 0.5f;


    [SerializeField] private float attackStartRadius = 4f;
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private UnityEvent onAttack;
    private float nextAttackTime;


    // A* route state: path nodes are followed in order, then the hunter arrives at pathGoal.
    private Pathfinding pathfinder;
    private List<Node> currentPath = new List<Node>();
    private int currentWaypointIndex;
    private Vector2 pathGoal;
    private float nextPathRefreshTime;
    private bool pathAvailable;

    // Static list so all hunters can find and track each other automatically
    private static System.Collections.Generic.List<HunterAI> allHunters = new System.Collections.Generic.List<HunterAI>();

    private void OnEnable() { allHunters.Add(this); }
    private void OnDisable() { allHunters.Remove(this); }
    private GridManager gridManager;


    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Crucial for top-down games so gravity doesn't pull them down
        rb.gravityScale = 0f; 
        // Automatically find the GridManager in the scene if it isn't assigned
        gridManager = FindAnyObjectByType<GridManager>();
        // Pathfinding uses the same GridManager that creates the walkable tile map.
        pathfinder = gridManager != null ? gridManager.GetComponent<Pathfinding>() : null;

        // Use the Inspector reference when set; otherwise look up the object tagged Player.
        GameObject playerObject = player != null ? player.gameObject : GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            if (playerRb == null)
                playerRb = playerObject.GetComponent<Rigidbody2D>();
        }

        if (pathfinder == null) Debug.LogWarning("HunterAI could not find a Pathfinding component on the GridManager.", this);
        
        ChooseWanderDirection();
    }

    

    private Vector2 BasicSeek(Transform target)
    {
        //find position to target and then scales by the offset to get the desired velocity
        Vector2 offset = (Vector2)target.position - rb.position;
        Vector2 desired = offset.sqrMagnitude > 0f ? offset.normalized * maxSpeed : Vector2.zero;
        //the desired velocity is the steering force towards the player 
        return desired - velocity;
    }
    
    //this method can replace  the basic seek
    private Vector2 Pursuit(Transform target, Rigidbody2D targetRb)
    {
        
        Vector2 targetPos = target.position;
        Vector2 targetVelocity = targetRb != null ? targetRb.linearVelocity : Vector2.zero;
        //distance from hunter to player
        float distanceToPlayer = Vector2.Distance(rb.position, targetPos);
        //predict future time based on dist and speed
        float predictionTime = maxSpeed >0f ? distanceToPlayer / maxSpeed :0f;
        //predict future position 
        Vector2 futurePosition = targetPos + (targetVelocity * predictionTime);
        //hunter goes towards the future position
        Vector2 offset = futurePosition - rb.position;
        Vector2 desired = offset.sqrMagnitude > 0f ? offset.normalized * maxSpeed : Vector2.zero;
        return desired - velocity;
        
    }

    //slow down once arriving to the target
    private Vector2 Arrive(Transform target)
    {
        Vector2 toTarget = (Vector2)target.position - rb.position;
        float distance = toTarget.magnitude;
        float speed = slowingRadius > 0f
            ? Mathf.Min(maxSpeed, maxSpeed * (distance / slowingRadius))
            : maxSpeed;
        Vector2 desired = distance > 0f ? toTarget / distance * speed : Vector2.zero;
        return desired - velocity;
    }

    private void Update()
    {
        float playerDistance = player != null
            ? Vector2.Distance(rb.position, (Vector2)player.position)
            : float.PositiveInfinity;

        Vector2 steering;

        if (player != null && playerDistance <= detectionRadius)
        {
            if (playerDistance <= attackRange)
            {
                TryAttackPlayer();
                steering = -velocity;
            }
            else
            {
                if (Time.time >= nextPathRefreshTime)
                    FindPathToPlayer();

                steering = FollowPath();
            }
        }
        else
        {
            if (Time.time >= nextWanderDirectionTime)
                ChooseWanderDirection();

            Vector2 desiredVelocity = wanderDirection * maxSpeed;
            steering = desiredVelocity - velocity;

            Vector2 avoidanceSteering = Avoidance();
            if (avoidanceSteering != Vector2.zero)
                steering += avoidanceSteering;
        }

        ApplySteering(steering);
    }

        

    private void TryAttackPlayer()
    {
        if (Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + Mathf.Max(0.1f, attackCooldown);
        onAttack?.Invoke();
    }

    //steer smothly instead of sharply changing direction
    private void ApplySteering(Vector2 steering)
    {
        steering = Vector2.ClampMagnitude(steering, maxSteeringForce);
        velocity = Vector2.ClampMagnitude(velocity + steering * Time.deltaTime, maxSpeed);
        
        // Directly set the Rigidbody's velocity. 
        // Unity physics will now move the hunter and block it against walls automatically!
        rb.linearVelocity = velocity;
    }

    //decides where the hunter goes when wandering around 'aimlessly'
    private void ChooseWanderDirection()
    {
        wanderDirection = Random.insideUnitCircle.normalized;
        if (wanderDirection.sqrMagnitude < 0.001f)
        {
            wanderDirection = Vector2.right;
        }

        nextWanderDirectionTime = Time.time + Mathf.Max(0.1f, wanderDirectionInterval);
    }

    private Vector2 Avoidance()
    {
        Vector2 avoidanceForce = Vector2.zero;
        
        //if the hunter is not moving at all it is 0
        if(velocity.sqrMagnitude < 0.001f) return avoidanceForce;

        //normalize the movement direction
        Vector2 forwardDir = velocity.normalized;

        //left and right feelers - calc rotated directions, so they dont check right in front
        Vector2 leftDir = Quaternion.Euler(0, 0, 45) * forwardDir;
        Vector2 rightDir = Quaternion.Euler(0, 0, -45) * forwardDir;

        Debug.DrawRay(transform.position, forwardDir * feelerLength, Color.red);
        Debug.DrawRay(transform.position, leftDir * (feelerLength * 0.75f), Color.yellow);
        Debug.DrawRay(transform.position, rightDir * (feelerLength * 0.75f), Color.yellow);
        //

        //the actual feelers
        RaycastHit2D hitAhead = Physics2D.Raycast(transform.position, forwardDir, feelerLength, obstacleLayer);
        RaycastHit2D hitLeft = Physics2D.Raycast(transform.position, leftDir, feelerLength * 0.75f, obstacleLayer);
        RaycastHit2D hitRight = Physics2D.Raycast(transform.position, rightDir, feelerLength * 0.75f, obstacleLayer);
        
        //if wall detected push away from it
        if(hitAhead.collider != null)
        {
            avoidanceForce += hitAhead.normal * maxSteeringForce;
        }
        if(hitLeft.collider != null)
        {
            avoidanceForce += hitLeft.normal * maxSteeringForce;
        }
        if(hitRight.collider != null)
        {
            avoidanceForce += hitRight.normal * maxSteeringForce;
        }
        return avoidanceForce;
    }
    
    //methods for the zombie surround
    private Vector2 CalculateSurroundForce() //calc at which point to orbit around player
    {
        if (player == null) return Vector2.zero;

        // calc a unique angle around the player based on index for each hunter
        float angle = ((float)hunterIndex / totalHunters) * 360f;
        float radian = angle * Mathf.Deg2Rad;

        //Determine the orbit point around the player, convert into actual direction vector from the angle 
        Vector2 vectorFromAngle = new Vector2(Mathf.Cos(radian), Mathf.Sin(radian));
        //so the hunters do not walk onto the player
        Vector2 orbitPoint = (Vector2)player.position + (vectorFromAngle * orbitDistance);

        //Arrive at that point where player is
        Vector2 arriveForce = ArriveAtPoint(orbitPoint);
        //not to bump into another hunter
        Vector2 separationForce = CalculateSeparation();

        return arriveForce + (separationForce * separationWeight);
    }

    private Vector2 ArriveAtPoint(Vector2 targetPos)
    {
        //calc the target where to surround the player (on the orbit point)
        Vector2 toTarget = targetPos - rb.position;
        float distance = toTarget.magnitude;
        //move faster if far away from player
        float speed = slowingRadius > 0f
            ? Mathf.Min(maxSpeed, maxSpeed * (distance / slowingRadius))
            : maxSpeed;
        //smooth movement - no jittering
        Vector2 desired = distance > 0f ? toTarget / distance * speed : Vector2.zero;
        return desired - velocity;
    }

    private Vector2 CalculateSeparation()
    {
        Vector2 steering = Vector2.zero;
        int count = 0;
        //scan all hunters in the game, ommit the other ones
        foreach (var other in allHunters)
        {
            if (other == this || other == null || other.rb == null) continue;

            float distance = Vector2.Distance(rb.position, other.rb.position);
            //if another hunter is within separation radius then push them away in another direction
            if (distance < separationRadius && distance > 0f)
            {
                Vector2 awayDir = (rb.position - other.rb.position).normalized;
                steering += awayDir / distance; // Closer objects push away harder
                count++;
            }
        }

    if (count > 0)
    {
        steering /= count;
        steering = steering.normalized * maxSpeed - velocity;
    }

    return steering;
    }


    private void FindPathToPlayer()
{
    nextPathRefreshTime = Time.time + Mathf.Max(0.1f, pathRefreshInterval);
    currentPath.Clear();
    currentWaypointIndex = 0;
    pathAvailable = false;

    if (gridManager == null || pathfinder == null || player == null)
        return;

    pathGoal = player.position;
    Node goalNode = gridManager.NodeFromWorldPoint(pathGoal);

    if (goalNode == null || !goalNode.walkable)
        return;

    List<Node> path = pathfinder.FindPath(rb.position, pathGoal);
    if (path != null)
    {
        currentPath = path;
        pathAvailable = true;
    }
}

    private Vector2 FollowPath()
    {
        // Brake if A* could not produce a route.
        if (!pathAvailable)
            return -velocity;

        // Skip waypoints already reached; the distance check absorbs small movement offsets.
        while (currentWaypointIndex < currentPath.Count &&
            Vector2.Distance(rb.position, currentPath[currentWaypointIndex].worldPosition) < 0.1f)
        {
            currentWaypointIndex++;
        }

        if (currentWaypointIndex < currentPath.Count)
        {
            // Steer toward the next tile center; ApplySteering handles acceleration and speed limits.
            Vector2 toWaypoint = currentPath[currentWaypointIndex].worldPosition - rb.position;
            Vector2 desiredVelocity = toWaypoint.sqrMagnitude > 0f
                ? toWaypoint.normalized * maxSpeed
                : Vector2.zero;
            return desiredVelocity - velocity;
        }

        // The path ends at a grid cell center; finish smoothly at the exact flanking position.
        return ArriveAtPoint(pathGoal);
    }

    private Vector2 GetOrbitPoint(Vector2 targetPosition)
    {
        // Space hunters evenly around the player based on their Inspector-assigned index.
        float angle = ((float)hunterIndex / Mathf.Max(1, totalHunters)) * 360f;
        float radians = angle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        return targetPosition + direction * orbitDistance;
    }
    
}