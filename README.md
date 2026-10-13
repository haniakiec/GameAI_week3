# Escape Lab 2D | Week 1 | Minimal Unity 6 project

**This is a real, saved 2D scene. It does not generate the level at runtime.**

## Open and play

1. Extract this ZIP.
2. In Unity Hub, choose **Add project from disk** and select `EscapeLab_2D_Unity6_MINIMAL`.
3. Use Unity **6**. Wait for assets and scripts to finish importing.
4. Open **Assets > Scenes > EscapeLab2D** (double-click `EscapeLab2D.unity`).
5. Select the **Game** tab and press **Play**.
6. Click inside the Game view if the keyboard does not respond. Move with **WASD** or **arrow keys**.

**Goal:** collect the 10 gold energy cells, then touch the exit when it turns green. You start with 3 health and have 180 seconds. The red Hunter is stationary and takes 1 health roughly once per second if you stand on it. Press **R** after a win or loss to restart.

## Only TWO gameplay scripts

| Script | Found on | What students learn |
|---|---|---|
| `PlayerController2D.cs` | Player | input, `Update`, `FixedUpdate`, `Rigidbody2D.MovePosition`, collision triggers |
| `GameManager.cs` | Game Manager | variables, methods, timer, win/lose rules, score, on-screen text |

All other game objects are just **sprites and 2D colliders configured in the scene**. The Hunter needs no script because it has no AI yet. The UI uses a handful of `OnGUI` statements so no Canvas setup is needed.

## What to inspect in the Unity Editor

- **Main Camera**: Orthographic projection, pointing straight at the 2D XY playing field.
- **Player**: `SpriteRenderer`, `Rigidbody2D`, `CircleCollider2D`, `PlayerController2D`.
- **Walls**: `SpriteRenderer` and non-trigger `BoxCollider2D`.
- **Gold energy cells**: `SpriteRenderer`, trigger `CircleCollider2D`, tag `Cell`.
- **Red Hunter**: `SpriteRenderer`, trigger `CircleCollider2D`, tag `Hunter`.
- **Exit**: `SpriteRenderer`, trigger `BoxCollider2D`, tag `Exit`.
- **Game Manager**: one `GameManager` script; it holds the game settings and exit sprite references.

## Change the rules without changing code

Select **GAME MANAGER** in the Hierarchy. In the Inspector you can change `Required Cells`, `Starting Health`, or `Game Seconds`. For a smaller goal, lower `Required Cells` (but keep it above zero). Select **PLAYER** and change `Move Speed`.

## Notes

- The project uses Unity 6's **built-in 2D physics** and **legacy keyboard input** (Project Settings > Player > Active Input Handling: Input Manager). No external packages or downloads are required.
- The level is built entirely from objects saved in `EscapeLab2D.unity`, making it inspectable and editable before pressing Play.
- If the Game view is unusually narrow, maximise it or choose 16:9. The saved camera has a generous orthographic size for common desktop layouts.
- This is the Week 1 foundation. Do not add steering or pathfinding yet: the stationary Hunter is intentional.
- See `STUDENT_GUIDE.md` for a code walkthrough and suggested mini-exercises.
- Actual compilation and Play mode require the Unity Editor, which is unavailable in this build environment.
