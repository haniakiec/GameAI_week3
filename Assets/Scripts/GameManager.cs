
using UnityEngine; // Gives access to Unity's built-in features.
using UnityEngine.SceneManagement; // Allows us to reload the game scene.

namespace EscapeLab2D // Groups our game scripts together.
{
    /// <summary>
    /// GAME MANAGER: Controls the rules of Escape Lab.
    ///
    /// It is responsible for:
    /// 1. Tracking collected cells, health, score and time.
    /// 2. Deciding when the exit unlocks.
    /// 3. Checking whether the player wins or loses.
    /// 4. Displaying game information and restarting the level.
    ///
    /// Why separate this from PlayerController2D?
    /// The Player Controller handles movement and collisions.
    /// GameManager handles the rules. This keeps both scripts simple.
    /// MonoBehaviour is Unity's base class for scripts attached to GameObjects, 
    /// giving you access to built-in event functions such as 
    /// Awake(), Start(), Update(), FixedUpdate(), LateUpdate(), OnEnable(), OnDisable(), 
    /// OnTriggerEnter(), OnCollisionEnter(), and many others.. 
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // =====================================================
        // 1. GAME SETTINGS
        // =====================================================

        // Public variables appear in Unity's Inspector.
        // int = whole number; float = number with decimal places.
        [Header("Game rules (try changing these)")]
        public int requiredCells = 11;     // Cells needed to unlock the exit.
        public int startingHealth = 5;     // How many hits the player can take.
        public float gameSeconds = 100f;  // Time limit: 180 seconds = 3 minutes.

        [Header("Exit: assigned in the Unity scene")]
        public SpriteRenderer exitRenderer; // Displays the exit's image.
        public Sprite unlockedExitSprite;   // Image used when the exit unlocks.

        // =====================================================
        // 2. GAME STATE
        // =====================================================

        // A property lets other scripts read information safely.
        // { get; private set; } means:
        // Other scripts can READ it, but only GameManager can CHANGE it.
        //
        // bool stores true or false. Initially the game is running.
        public bool Playing { get; private set; } = true;

        public int Cells { get; private set; }  // Number of collected cells.
        public int SpecialCells { get; private set; }  // Number of collected  special cells.

        public int Health { get; private set; } // Player's remaining health.

        // => means "calculate and return".
        // Score is calculated whenever requested, not stored separately.
        public int Score => Cells * 100 + SpecialCells *200; // Each cell is worth 100 points.

        // Private variables are used only inside this script.
        private float secondsLeft;  // Remaining time in seconds.
        private string result = ""; // Text describing the game's outcome.
                                     // string stores text; "" means empty text.


        // =====================================================
        // 3. START: INITIALISE THE GAME
        // =====================================================

        /// <summary>
        /// START: Called by Unity before the first Update().
        ///
        /// Unlike Awake(), which runs when the script is loaded,
        /// Start() runs just before the script begins updating.
        ///
        /// We use it to give the game its starting values.
        /// </summary>
        private void Start()
        {
            Health = startingHealth;  // Give the player full starting health.
            secondsLeft = gameSeconds; // Start the countdown at 180 seconds.
        }


        // =====================================================
        // 4. UPDATE: COUNT TIME AND HANDLE RESTART
        // =====================================================

        /// <summary>
        /// UPDATE: Called once every rendered frame.
        ///
        /// It checks whether the game is running:
        /// - If finished, wait for R to restart.
        /// - Otherwise, decrease the remaining time.
        ///
        /// We use Time.deltaTime so the countdown measures
        /// real elapsed game time, not the number of frames.
        /// </summary>
        private void Update()
        {
            // ! means NOT. So !Playing means the game has ended.
            if (!Playing)
            {
                // GetKeyDown is true only when R is first pressed.
                // Unlike GetKey, it does not remain true while held.
                if (Input.GetKeyDown(KeyCode.R))
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);

                // LoadScene reloads the current level, resetting the game.
                // return stops the rest of Update() from running.
                return;
            }

            // deltaTime is the number of seconds since the last frame.
            //
            // Example: at 60 FPS, deltaTime is approximately 0.0167.
            // Subtracting it every frame creates a countdown that
            // works regardless of the computer's frame rate.
            secondsLeft -= Time.deltaTime;

            // <= means less than or equal to.
            // When no time remains, end the game.
            if (secondsLeft <= 0f) EndGame("TIME UP!");
        }


        // =====================================================
        // 5. COLLECTCELL: COLLECT AN ENERGY CELL
        // =====================================================

        /// <summary>
        /// Called by PlayerController2D when a cell is collected.
        /// Increases the cell count and unlocks the exit if ready.
        ///
        /// public means another script can call this function.
        /// void means the function does not return a value.
        /// </summary>
        public void CollectCell()
        {
            // Do nothing if the game has ended.
            if (!Playing) return;

            // ++ increases the number by one.
            // For example, Cells goes from 3 to 4.
            Cells++;

            // && means AND: both conditions must be true.
            // >= means greater than or equal to.
            // != null checks that the exit reference exists.
            if (Cells >= requiredCells && exitRenderer != null)
            {
                // If an unlocked sprite was assigned, display it.
                // Otherwise, change the existing exit colour to green.
                if (unlockedExitSprite != null) exitRenderer.sprite = unlockedExitSprite;
                else exitRenderer.color = Color.green;
            }
        }


        // =====================================================
        // 6. HITPLAYER: REDUCE HEALTH
        // =====================================================

        /// <summary>
        /// Called by PlayerController2D when the Hunter hits us.
        /// Reduces health and checks whether the player has lost.
        /// </summary>
        public void HitPlayer()
        {
            if (!Playing) return;

            Health--; // -- subtracts one from the current health.

            // If health reaches zero (or below), the player loses.
            if (Health <= 0) EndGame("NO HEALTH LEFT!");
        }


        // =====================================================
        // 7. TOUCHEXIT: CHECK WHETHER THE PLAYER WINS
        // =====================================================

        /// <summary>
        /// Called when the player touches the exit.
        ///
        /// Touching the exit is not enough to win.
        /// The player must first collect the required cells.
        /// </summary>
        public void TouchExit()
        {
            // Win only if the game is running AND enough cells
            // have been collected. Otherwise, nothing happens.
            if (Playing && Cells >= requiredCells) EndGame("YOU ESCAPED!");
        }


        // =====================================================
        // 8. ENDGAME: FINISH THE GAME
        // =====================================================

        /// <summary>
        /// Ends the game and records the result.
        ///
        /// Parameter: string message
        /// A parameter is information passed into a function.
        ///
        /// Example: EndGame("TIME UP!") passes that text
        /// into the function, where it becomes 'message'.
        ///
        /// Keeping this logic in one function avoids repeating
        /// the same code for each win or loss condition.
        /// </summary>
        private void EndGame(string message)
        {
            // Stop normal gameplay.
            // The other functions check Playing before acting.
            Playing = false;

            // Store the message to display on screen.
            result = message;

            // Debug.Log writes information to Unity's Console.
            //
            // $"..." is string interpolation: expressions inside
            // { } are replaced with their values.
            //
            // Mathf.Max(0f, secondsLeft) prevents negative time.
            // :0.0 displays the time with one decimal place.
            // + joins two pieces of text together.
            Debug.Log($"Escape Lab | {result} | Cells: {Cells} | Score: {Score} | " +
                      $"Time used: {gameSeconds - Mathf.Max(0f, secondsLeft):0.0}s");
        }


        // =====================================================
        // 9. ONGUI: DISPLAY GAME INFORMATION
        // =====================================================

        /// <summary>
        /// ONGUI: Unity calls this to draw a simple interface.
        ///
        /// It can be called several times per frame for different
        /// GUI events, unlike Update(), which runs once per frame.
        ///
        /// We use it to display health, score, time and messages.
        ///
        /// Why not use a Canvas?
        /// OnGUI is sufficient for this small teaching project.
        /// Larger games normally use Unity's modern UI systems.
        /// </summary>
        private void OnGUI()
        {
            // Change the font size of labels and boxes.
            GUI.skin.label.fontSize = 19;
            GUI.skin.box.fontSize = 19;

            // Draw a box containing our game information.
            //
            // Rect(x, y, width, height) specifies its position
            // and size in screen pixels.
            //
            // For GUI coordinates, (0, 0) is the top-left corner.
            // An empty string "" means the box has no text.
            GUI.Box(new Rect(12, 12, 270, 160), "");

            // GUI.Label displays text at a chosen screen position.
            //
            // \n starts a new line.
            // {variable} inserts the current value of a variable.
            //
            // Mathf.Max prevents displaying negative time.
            // CeilToInt rounds UP to the next whole number.
            //
            // condition ? A : B means:
            // If true, use A; otherwise, use B.
            // This is called the conditional operator.
            GUI.Label(new Rect(28, 20, 245, 150),
                $"ESCAPE LAB 2D\nCells: {Cells}/{requiredCells}\n" +
                $"Health: {Health}    Score: {Score}\n" +
                $"Time: {Mathf.CeilToInt(Mathf.Max(0f, secondsLeft))}s\n" +
                (Cells >= requiredCells ? "EXIT OPEN" : "EXIT LOCKED"));

            // Screen.height gives the height of the game window.
            // Subtracting 36 positions the instructions near the bottom.
            GUI.Label(new Rect(14, Screen.height - 36, 560, 32),
                "Move: WASD / arrow keys    Collect gold cells    Avoid red");

            // Show the result only when the game has ended.
            if (!Playing)
                // Screen.width / 2 and Screen.height / 2 help
                // position this message box in the screen centre.
                GUI.Box(new Rect(Screen.width / 2f - 170, Screen.height / 2f - 50, 340, 100),
                    result + "\nPress R to restart");
        }
    }
}
