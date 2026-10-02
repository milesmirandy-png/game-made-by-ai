The game builds everything at runtime, so the only scene it needs is an empty
one with a `GameManager` in it (or any scene at all: `GameBootstrap` adds the
`GameManager` when you press Play).

`Boot.unity` is created here automatically the first time the project is
opened in the editor, and added to Build Settings. If it is missing, use the
menu **SWAT -> Create Boot Scene**.

Maps (office, warehouse, apartment, training) and the headquarters are built
by code in `Scripts/Environment/*Map.cs` inside that one scene.
