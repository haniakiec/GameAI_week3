using System.Collections;
using UnityEngine; // Gives access to Unity's built-in features.
//using UnityEngine.InputSystem;
using System.Collections.Generic;
namespace EscapeLab2D 
{
    /// <summary>
    /// Controls the player:
    /// 1. Reads keyboard input.
    /// 2. Moves the player using 2D physics.
    /// 3. Detects energy cells, the Hunter and the exit.
    ///
    /// GameManager handles the game rules, such as health and scoring.
    /// </summary>

    // Automatically adds these components if they are missing.
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class PlayerController2D : MonoBehaviour
    {
        // A class groups related variables and functions.
        // MonoBehaviour allows Unity to run our functions automatically.

        [Header("Player settings")] // Shows a heading in the Inspector.

        // Public variables can be changed in Unity's Inspector.
        // float means a number that can contain decimal places.
        public float moveSpeed = 6f;          // Movement speed in units/second.
        public float powerUp = 0f; 
        public float secondsBetweenHits = 1f; // Minimum delay between hits.
        public float coolDown = 5f;

        // Private variables store information used by this script.
        private Rigidbody2D body;   // Handles movement and 2D physics.
        private GameManager game;   // Gives access to the game rules.
        private Vector2 direction;  // Direction (X, Y), e.g. (1, 0) = right.
        private float nextHitTime;  // Earliest time the next hit is allowed.
        private float nextPowerUpTime; // Earliest time sprint can be used again.
        private bool sprinting;

        [SerializeField] private Pathfinding pathfinder;
        [SerializeField] private Transform exitTransform;

        private Coroutine movementCoroutine;

        // Call this to start moving to a destination (like the exit or player)
        public void MoveToTarget(Vector2 destination)
        {
            if (pathfinder == null)
            {
                Debug.LogWarning("Pathfinder is not assigned.");
                return;
            }

            // 1. Get the path from Pathfinding.cs
            List<Node> path = pathfinder.FindPath(transform.position, destination);

            if (path != null && path.Count > 0)
            {
                // Stop any previous movement so they don't overlap
                if (movementCoroutine != null)
                {
                    StopCoroutine(movementCoroutine);
                }

                // Start following the new path
                movementCoroutine = StartCoroutine(FollowPathRoutine(path));
            }
            else
            {
                Debug.Log("No path found to destination!");
            }
        }

        private IEnumerator FollowPathRoutine(List<Node> path)
        {
            if (path == null || path.Count == 0)
            {
                yield break;
            }

            int targetIndex = 0;
            Vector2 currentWaypoint = path[targetIndex].worldPosition;

            while (true)
            {
                // Move towards the current node's world position
                transform.position = Vector2.MoveTowards(transform.position, currentWaypoint, moveSpeed * Time.deltaTime);

                // Check if we are close enough to the current waypoint to switch to the next one
                if (Vector2.Distance(transform.position, currentWaypoint) < 0.1f)
                {
                    targetIndex++;

                    // If we reached the end of the path, stop moving
                    if (targetIndex >= path.Count)
                    {
                        yield break;
                    }

                    currentWaypoint = path[targetIndex].worldPosition;
                }

                yield return null; // Wait for the next frame
            }
        }

        public void CalculateRouteToExit()
        {
            if (pathfinder == null || exitTransform == null)
            {
                Debug.LogWarning("Pathfinder or exit transform is not assigned.");
                return;
            }

            // Get path from current position to the exit position
            List<Node> path = pathfinder.FindPath(transform.position, exitTransform.position);

            if (path != null)
            {
                Debug.Log($"Path found! Total steps to exit: {path.Count}");

                // You can now loop through 'path' nodes to make your character move along node.worldPosition points!
            }
            else
            {
                Debug.Log("No valid path to the exit found.");
            }
        }


        /// <summary>
        /// AWAKE: Called by Unity when this script is first loaded.
        /// Used to find the components we need before gameplay.
        /// This avoids searching for them every frame.
        /// </summary>
        private void Awake()
        {
            // GetComponent finds a component on this GameObject.
            body = GetComponent<Rigidbody2D>();

            // Finds any active GameManager in the scene.
            // We only need one, so there is no need to search in a particular order.
            game = FindObjectOfType<GameManager>();
        }


        /// <summary>
        /// UPDATE: Called once per rendered frame (e.g. 60 times/second at 60 FPS).
        /// Used to READ keyboard input, not to move the physics object.
        ///
        /// Why? The player can press a key at any moment, so we check
        /// input every frame. We save the direction for FixedUpdate().
        /// </summary>
        private void Update()
        {
            // Start each frame with no movement (0, 0).
            // Otherwise the player could keep moving after releasing a key.
            direction = Vector2.zero;

            // Stop if GameManager is missing OR the game is not running.
            // == means equal to, || means OR, ! means NOT.
            if (game == null || !game.Playing) return;

            
            if (Input.GetKeyDown(KeyCode.LeftShift) && Time.time >= nextPowerUpTime)
                sprinting = true;

            if (Input.GetKeyUp(KeyCode.LeftShift))
            {
                if (sprinting)
                    nextPowerUpTime = Time.time + coolDown;

                sprinting = false;
            }
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (exitTransform != null)
                {
                    MoveToTarget(exitTransform.position);
                }
            }
            powerUp = sprinting ? 10f : 0f;



            // GetKey is true while a key is held down.
            // ++ adds 1; -- subtracts 1.
            // X controls left/right, Y controls up/down.
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) direction.y++;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) direction.y--;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) direction.x++;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) direction.x--;
            

            // Without this, moving diagonally would be about 41% faster.
            // Normalising keeps the same direction but limits its length to 1.
            direction = direction.normalized;
        }


        /// <summary>
        /// FIXEDUPDATE: Called at regular physics intervals (usually 50/second).
        ///
        /// Why not move in Update()?
        /// Update() depends on the frame rate, which can change.
        /// FixedUpdate() uses a regular physics timestep, making
        /// physics movement and collisions more consistent.
        ///
        /// In short: Update() reads input; FixedUpdate() applies movement.
        /// </summary>
        private void FixedUpdate()
        {
            // Distance = Direction * Speed * Time.
            // fixedDeltaTime is the duration of one physics step.
            // MovePosition asks Unity's 2D physics system to move the player.
            body.MovePosition(body.position + direction * (moveSpeed+powerUp) * Time.fixedDeltaTime);
        }


        /// <summary>
        /// ONTRIGGERENTER2D: Called when the player enters a 2D trigger.
        ///
        /// 'other' is the collider of the object we touched.
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (game == null || !game.Playing) return;

            // Tags are labels assigned to objects in Unity's Inspector.
            if (other.CompareTag("Cell"))
            {
                game.CollectCell();              // Update the score and cell count.
                other.gameObject.SetActive(false); // Hide and disable the cell.
            }
            if (other.CompareTag("SpecialCell"))
            {
                game.CollectSpecialCell();              // Update the score and cell count.
                other.gameObject.SetActive(false); // Hide and disable the cell.
            }
            if (other.CompareTag("Potion"))
            {
                game.CollectPotion();              // Update the score and cell count.
                other.gameObject.SetActive(false); // Hide and disable the cell.
            }
            if (other.CompareTag("Poison"))
            {
                game.CollectPoison();              // Update the score and cell count.
                if (playerVisual != null)
                    StartCoroutine(ShakePlayer());

                other.gameObject.SetActive(false); // Hide and disable the cell.
            }
        }


        /// <summary>
        /// ONTRIGGERSTAY2D: Called repeatedly while the player remains inside a trigger.
        ///
        /// Stay keeps checking while objects overlap, allowing repeated Hunter damage
        /// and repeated exit checks.
        /// </summary>
    
       private void OnCollisionStay2D(Collision2D collision)
    {
        if (game == null || !game.Playing) return;

        // Check if the player is touching the solid Hunter
        if (collision.gameObject.CompareTag("Hunter") && Time.time >= nextHitTime)
        {
            game.HitPlayer(); // Reduce health through GameManager.

            if (playerVisual != null)
                StartCoroutine(ShakePlayer());

            nextHitTime = Time.time + secondsBetweenHits;
        }
    }

        // Handles trigger-based objects like the Exit (Is Trigger = true)
        private void OnTriggerStay2D(Collider2D other)
        {
            if (game == null || !game.Playing) return;

            // Check the exit repeatedly while touching it
            if (other.CompareTag("Exit")) 
            {
                game.TouchExit();
            }
        }

        [SerializeField] private Transform playerVisual;
        [SerializeField] private float shakeDuration = 0.15f;
        [SerializeField] private float shakeMagnitude = 0.08f;

        private IEnumerator ShakePlayer()
        {
            Vector3 originalPosition = playerVisual.localPosition;
            float elapsed = 0f;

            while (elapsed < shakeDuration)
            {
                Vector2 offset = Random.insideUnitCircle * shakeMagnitude;
                playerVisual.localPosition = originalPosition + (Vector3)offset;
                elapsed += Time.deltaTime;
                yield return null;
            }

            playerVisual.localPosition = originalPosition;
        }
    }
}
