There are no prefab files: characters, weapons, doors, props, the van and
effects are built by code factories at runtime, so the project works without
any editor setup.

- People: `Scripts/Core/CharacterFactory.cs` (used by `PlayerController.Spawn`, `SquadAI.Spawn`, `EnemyAI.Spawn`, `CivilianAI.Spawn`)
- Weapons: `Scripts/Weapons/WeaponModels.cs` (a `WeaponData.modelPrefab` assigned in the Inspector replaces the block model)
- Doors: `Scripts/Environment/DoorController.cs` (`DoorController.Create`)
- Walls, rooms and props: `Scripts/Environment/LevelBuilder.cs`
- Van: `Scripts/Vehicles/VanBuilder.cs`
