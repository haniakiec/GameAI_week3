using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class HunterAI : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float maxSpeed = 4f;
    [SerializeField] private float maxSteeringForce = 8f;
    [SerializeField] private float detectionRadius = 10f;
    [SerializeField] private float slowingRadius = 2f;
    [SerializeField] private float wanderDirectionInterval = 2f;

    //for predicting the players movement
    [Header("Target Settings")]
    [SerializeField] private Rigidbody2D playerRb; 
    private Rigidbody2D rb;
    private Vector2 velocity;
    private Vector2 wanderDirection;
    private float nextWanderDirectionTime;
    [Header("Feeler / Avoidance Settings")]
    [SerializeField] private float feelerLength = 1.5f;
    [SerializeField] private LayerMask obstacleLayer; // Assign your Wall layer in the Inspector

    [Header("Surround & Flanking Settings")]
    [SerializeField] private int hunterIndex = 0;      // Set this to 0 for Hunter 1, and 1 for Hunter 2 in the Inspector!
    [SerializeField] private int totalHunters = 2;     
    [SerializeField] private float orbitDistance = 2.5f; 
    [SerializeField] private float separationRadius = 1.5f;
    [SerializeField] private float separationWeight = 2f;

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
         gridManager = FindObjectOfType<GridManager>();

        if (player == null)
        {
            GameObject playerObject = GameObject.FindWithTag("Player");
            player = playerObject.transform;
            playerRb = playerObject.GetComponent<Rigidbody2D>();
        }

        ChooseWanderDirection();
    }

    private void Update()
    {
        HunterUpdate();
    }

    private Vector2 BasicSeek(Transform target)
    {
        Vector2 offset = (Vector2)target.position - rb.position;
        Vector2 desired = offset.sqrMagnitude > 0f ? offset.normalized * maxSpeed : Vector2.zero;
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

    private void HunterUpdate()
    {
        Vector2 steering;
        //if player in detection radius then do not wander anymore, perform basic seek
        if (player != null && Vector2.Distance(rb.position, (Vector2)player.position) < detectionRadius)
        {
            steering =  CalculateSurroundForce();
        }
        else
        {
            if (Time.time >= nextWanderDirectionTime)
            {
                ChooseWanderDirection();
            }

            steering = wanderDirection * maxSpeed - velocity;
        }
        Vector2 avoidanceSteering = Avoidance();
        Vector2 finalSteering = steering;
        //combine them: Avoidance takes heavy priority if a wall is close
        if (avoidanceSteering != Vector2.zero)
        {
            finalSteering = avoidanceSteering * 2f; // Prioritize wall dodging over chasing the player
        }

        ApplySteering(finalSteering);
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
    //calc the target where to surround the playr (on the orbit point)
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
        if (other == this || other == null) continue;

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
// 1. Make sure you have a reference to your GridManager


private void FindPathToPlayer()
{
    if (gridManager == null || player == null) return;

    // 2. Identify the START node (where the hunter currently is)
    Node startNode = gridManager.NodeFromWorldPoint(rb.position);

    // 3. Identify the TARGET node (where the player currently is)
    Node targetNode = gridManager.NodeFromWorldPoint(player.position);

    // 4. (Optional check) Make sure the target node isn't inside a wall!
    if (!targetNode.walkable)
    {
        Debug.LogWarning("Player is standing on an unwalkable node/wall!");
        return;
    }

    // Now you can pass `startNode` and `targetNode` directly into your A* pathfinding script!
    // Example: List<Node> path = Pathfinding.FindPath(startNode, targetNode);
}
    
}