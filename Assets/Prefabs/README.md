Characters, props, doors and effects are built by code factories so the
project works without any editor setup:

- People: Scripts/Core/CharacterFactory.cs (+ PlayerController/EnemyAI/CivilianAI.Spawn)
- Doors: Scripts/World/DoorController.cs (DoorController.Create)
- Props and walls: Scripts/World/LevelBuilder.cs

Drop real prefabs here later if you want to replace the placeholder shapes.
