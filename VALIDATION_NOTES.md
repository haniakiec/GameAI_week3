# Structural validation

- Saved Unity 6 2D scene with orthographic camera.
- Exactly two gameplay C# scripts (PlayerController2D and GameManager).
- Visible sprites, player Rigidbody2D and 2D colliders are retained.
- Ten active collectable triggers, plus Hunter and Exit triggers, are tagged.
- TagManager declares Cell, Hunter and Exit.
- Scene references only the two retained C# script GUIDs.
- No editor-only scripts or runtime-level generator needed.
- Level content, win/loss rules and build scene are preserved.

**Limitation:** Unity Editor was not available for compilation or Play mode testing. The student/teacher guides describe the playtests to carry out locally.
