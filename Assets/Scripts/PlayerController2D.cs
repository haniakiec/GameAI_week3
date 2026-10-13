using System.Collections;
using UnityEngine; // Gives access to Unity's built-in features.
//using UnityEngine.InputSystem;

namespace EscapeLab2D // Groups our game scripts together.
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
            game = FindAnyObjectByType<GameManager>();
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
        /// ONTRIGGERENTER2D: Called when the player ENTERS a 2D trigger.
        /// We use it for collecting cells because each cell is collected once.
        ///
        /// 'other' is the collider of the object we touched.
        /// Triggers detect overlap without physically blocking movement.
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
                other.gameObject.SetActive(false); // Hide and disable the cell.
            }
        }


        /// <summary>
        /// ONTRIGGERSTAY2D: Called repeatedly during physics updates
        /// while the player REMAINS inside a trigger.
        ///
        /// Unlike Enter, Stay keeps checking while objects overlap.
        /// Useful for repeated Hunter damage and checking the exit.
        /// </summary>
        private void OnTriggerStay2D(Collider2D other)
        {
            if (game == null || !game.Playing) return;

            // && means AND: both conditions must be true.
            // Time.time is the elapsed game time in seconds.
            // >= means greater than or equal to.
            if (other.CompareTag("Hunter") && Time.time >= nextHitTime)
            {
                game.HitPlayer(); // Reduce health through GameManager.

                if (playerVisual != null)
                    StartCoroutine(ShakePlayer());

                nextHitTime = Time.time + secondsBetweenHits;
            }

            // Check the exit repeatedly while touching it.
            // GameManager decides whether enough cells were collected.
            if (other.CompareTag("Exit")) game.TouchExit();
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
