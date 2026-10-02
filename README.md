# SWAT: Clear the Building

A lightweight top-down tactical SWAT game made in Unity. Lead the raid on an
office building: breach doors, throw flashbangs, rescue civilians, and arrest
or neutralize armed suspects. Then get back to the van.

It's built to run on anything, from a potato laptop to a gaming PC. The game
picks a graphics level for your hardware and turns itself down if it starts
to stutter.

## How to Run

You need **Unity 2022.3 or newer** (Unity 6 recommended), installed through
[Unity Hub](https://unity.com/download). No extra packages are needed.

**Option A: open this folder as a project**

1. Download this repository (on GitHub: **Code → Download ZIP**) and unzip it.
2. In Unity Hub click **Add → Add project from disk** and pick the unzipped folder.
3. If Hub says the editor version isn't installed, click the version and choose the Unity version you have.
4. When the editor has opened, press **Play** ▶ at the top.

**Option B: copy it into a new project**

1. In Unity Hub create a new project (the **Universal 3D** or **3D (Built-In)** template both work).
2. Copy everything inside this repository's `Assets` folder into your project's `Assets` folder.
3. Press **Play** ▶.

The game builds the level when you press Play, in whatever scene is open, so
there is nothing to drag into the scene. Click inside the Game view so it gets
your keyboard and mouse.

If nothing happens when you press Play, open **Window → General → Console** and
look for red error messages.

## Controls

| Key | Action |
| --- | --- |
| **W A S D** | Move |
| **Mouse** | Aim |
| **Left click** | Fire, or use the selected equipment |
| **R** | Reload |
| **Shift** | Sprint (loud: suspects hear you) |
| **E** | Open/close doors, rescue civilians, arrest surrendered suspects |
| **Q** | Switch to the next weapon or equipment |
| **1 - 7** | Pick a slot: Pistol, SMG, Assault Rifle, Shotgun, Flashbang, Smoke, Breaching Charge |
| **F** | Shout "Police! Drop your weapon!" |
| **Mouse wheel** | Zoom |
| **Esc** | Pause menu (graphics settings, restart, quit) |
| **F3** | Show FPS |

## The mission

Objectives:

1. Enter the building
2. Rescue civilians (yellow rings)
3. Neutralize or arrest the suspects (red rings)
4. Secure the Storage Room and the Security Room (stand inside with no armed suspects left)
5. Return to the SWAT van (blue zone)

Tips:

- **Doors with yellow stripes are locked.** Stand next to one and press **E**
  (or select the breaching charge with **7** and click) to blow it open. Step
  back: the blast hurts.
- **Flashbangs** stun every suspect who can see the blast. **Smoke** blocks
  their view.
- **Shouting** makes suspects surrender, especially ones who haven't seen you,
  are stunned, or are hurt. Walk up to a surrendered suspect and press **E** to arrest them.
- Suspects hear gunshots, sprinting and doors. Walking is quiet.
- Scoring rewards tactics, not body count. Objectives, rescues and arrests earn
  points. Killing a suspect earns nothing extra. Hurting civilians or
  surrendered suspects costs points.

## Runs on anything

| Tier | What changes |
| --- | --- |
| **Potato** | World drawn at half resolution (HUD stays sharp), no shadows, no dynamic lights, fewer particles, AI thinks less often |
| **Low** | 75% resolution, no shadows |
| **Medium** | Full resolution, hard shadows, muzzle-flash lights |
| **High** | Soft shadows, 2x anti-aliasing, VSync |
| **Ultra** | Longer shadows, 4x anti-aliasing, more particles |

- The first launch picks a tier from your graphics card, memory and CPU.
- In **Auto** mode, if the frame rate stays under 40 FPS for a few seconds,
  the game drops a tier and tells you.
- You can pick a tier yourself on the briefing screen or in the pause menu.
  The choice is saved.

Under the hood:
- The level is plain boxes with one shared material per colour.
- Bullets are raycasts, not physics objects.
- Effects, sounds and grenades are reused from pools instead of being created and destroyed.
- All AI runs from one update loop, and each character only "thinks" a few times a second.

## Project layout

```
Assets/
├── Scenes/              (optional saved scene, see below)
├── Scripts/
│   ├── Core/            GameManager, GameBootstrap, CameraController, QualityManager,
│   │                    AudioManager, EffectsManager, GameInput, CharacterFactory, Shapes
│   ├── Player/          PlayerController, PlayerHealth, PlayerInteraction
│   ├── Weapons/         WeaponController, Weapon, WeaponData, EquipmentData,
│   │                    ThrownGrenade, SmokeCloud, Equipment
│   ├── AI/              AIManager, EnemyAI (state machine), EnemyController, EnemyHealth,
│   │                    EnemyWeapon, EnemyData, CivilianAI, AgentMover
│   ├── Missions/        MissionManager, Objective
│   ├── World/           LevelBuilder, LevelLayout, DoorController, NavMeshBaker
│   ├── UI/              UIManager
│   └── Editor/          SWAT menu (save a mission scene)
├── ScriptableObjects/Resources/
│   ├── Weapons/         Pistol, SMG, Assault Rifle, Shotgun
│   ├── Equipment/       Flashbang, Smoke Grenade, Breaching Charge
│   └── Enemies/         Suspect, Heavy
├── Prefabs/ Materials/ Models/ Audio/   (placeholders, everything is generated by code for now)
```

## Changing things

- **Weapons, equipment and enemies:** click the assets in
  `Assets/ScriptableObjects/Resources` and change the numbers in the Inspector
  (damage, fire rate, magazine size, spread, recoil, enemy accuracy, reaction
  time and so on). To add a weapon, use **Assets → Create → SWAT → Weapon** and
  save it in the `Weapons` folder.
- **The building:** `Scripts/World/LevelBuilder.cs`. Rooms, walls, doors,
  furniture, suspects and civilians are each one line. Copy
  `BuildClearTheBuilding` to make a new building.
- **Camera:** select the Main Camera while playing to tweak angle, zoom limits and look-ahead.

## Building a standalone game

1. Use the menu **SWAT → Create Mission Scene**. It saves
   `Assets/Scenes/Mission01_ClearTheBuilding.unity` and adds it to Build Settings.
2. **File → Build Settings → Build**.
