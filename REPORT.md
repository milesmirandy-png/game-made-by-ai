# SWAT: Tactical Response - Final Report

This report covers what was built, what was simplified, how it was checked,
and what is still unverified. The short version: the whole game is
implemented in C# and compiles in three configurations, but **it has not been
run inside the Unity editor yet**. No play-testing, no performance
measurements and no screenshots exist. Treat everything below as "implemented
in code" unless it says otherwise.

## Unity version, packages and configuration

| Item | Value |
| --- | --- |
| Unity version | Project file targets **Unity 6 (6000.0.23f1)**. Not opened in that editor during this work. |
| Packages | Built-in modules only (`Packages/manifest.json`): ai, animation, audio, imgui, jsonserialize, particlesystem, physics, screencapture, ui, uielements, umbra, unitywebrequest. No registry packages. |
| Render pipeline | No pipeline asset is required. Materials are copies of the active pipeline's default material, so the same code is meant to work in Built-in and URP. Only the code path exists; neither pipeline was tested. |
| Input | `GameInput` reads the classic Input Manager by default. If the Input System package is installed, the `Swat` assembly definition defines `SWAT_INPUT_SYSTEM` and the same bindings are read through the Input System instead. |
| Scenes | One runtime scene. The editor script creates `Assets/Scenes/Boot.unity` (an empty scene with the `GameManager`) the first time the project opens, or via **SWAT -> Create Boot Scene**. The game also starts in any scene through `GameBootstrap`. |
| Assembly | `Assets/Scripts/Swat.asmdef` (editor code is guarded with `#if UNITY_EDITOR`). |
| Save file | `Application.persistentDataPath/swat_tactical_response.json` (versioned JSON; corrupt files are backed up to `.bak` and replaced). |

## Architecture

Everything is created at runtime by code: the HQ diorama, the four maps,
characters, weapons, the van, materials, UI and sounds. `GameManager` swaps
between the HQ (behind the menus) and a freshly built mission map, bakes the
NavMesh for the map once at deployment (doors never trigger a rebake;
locked/wedged doors carve it with `NavMeshObstacle`), and drives the state
flow:

`MainMenu -> Headquarters -> Briefing -> OfficerSelection -> Loadout -> Deploying -> Playing <-> Paused -> Debrief`

The spec's script names map onto the code as follows (most exist with the same name):

| Spec name | Implementation |
| --- | --- |
| GameManager, AudioManager, SaveManager, ObjectPool | Same names (`Core/`, `Audio/`, `Save/`, `Utilities/`) |
| SceneLoader | Not a separate class: `GameManager` builds/destroys maps inside one scene (`BuildMission`, `ClearMission`) |
| PlayerController, PlayerHealth, PlayerInteraction | Same names |
| PlayerInputHandler | `Core/GameInput.cs` (remappable bindings, legacy or Input System backend) |
| CameraController | Same name |
| OfficerData, OfficerController, OfficerHealth, OfficerLoadout, OfficerSelectionManager | Same names |
| SquadAI, SquadCommandManager, SquadStatusTracker | Same names |
| SquadFormationController | `Squad/SquadFormation.cs` |
| WeaponData, Weapon, WeaponController, WeaponInventory, WeaponEffects | Same names (`WeaponInventory` is in `Weapon.cs`) |
| EnemyAI, EnemyController, EnemyHealth, EnemyWeapon, AIVisibility, CoverPoint | Same names |
| CivilianAI | Same name |
| CivilianInteraction | `CivilianAI` implements `IInteractable` (follow, wait, treat, free) |
| CivilianRescueTracker | Class in `Missions/MissionStats.cs` |
| TacticalEquipment, Flashbang, SmokeGrenade, BreachingCharge, DoorWedge, MedicalKit, FlashlightController | Same names (`Equipment/`), plus `ReconCamera`, `PortableLight` |
| DoorController, SecurityCamera, SecurityConsole, AlarmSystem, ExtractionZone, RoomController | Same names (`Environment/`) |
| MissionData, MissionManager, Objective, ObjectiveTracker, MissionScoring, MissionBriefing | Same names, plus `MissionRandomizer`, `TacticalIntel` |
| UIManager, MainMenuController, HUDController, MissionSelectionUI, OfficerSelectionUI, LoadoutUI, CommandWheelUI, TacticalMapUI, PauseMenuController, MissionDebriefUI | Same names, plus `BriefingUI`, `SettingsUI`, `UITheme`, `UIIcons`, `CharacterPreview` |
| IDamageable, IInteractable | `Core/Damage.cs` (plus `ICombatTarget` for anything suspects can shoot at) |

## Features implemented

These exist in code and compile. None has been exercised in Play Mode.

- **Identity and flow:** title "SWAT: TACTICAL RESPONSE", dark navy/black/muted-blue IMGUI theme with red/blue emergency accents; full flow from main menu to debriefing and back to HQ; restart, retry with same or new seed, return to HQ/menu, quit.
- **Player:** walk, sprint with stamina, crouch, steady aim, mouse-facing movement, footsteps, noise, flashlight, role ability, health, armor condition, movement state, interaction with hold progress.
- **Camera:** 55 degree angled follow camera, wheel zoom with min/max, three zoom presets (V), adjustable zoom sensitivity and look-ahead, map bounds, screen shake; showcase camera for menus.
- **Officers:** six fictional officers with name, callsign, role, portrait (drawn), 3D preview in HQ, stats (health, armor, speed, accuracy, reaction, perception, command responsiveness, capacity), personality, role ability, preferred kit, unlock rules, uniform colors.
- **Squad:** up to three AI officers; command wheel (hold Z, mouse or number keys) with 10 general orders and door orders (Stack up, Open/Breach/Flash & clear); individual (F1-F3) or group (F4) selection; follow formation, hold, regroup, move with waypoint queue, cover, stack and coordinated entry, stay behind (hold fire), return, assist civilians (treat, free, escort including down stairs), wait; stuck recovery; shout-then-shoot rules of engagement; reload and sidearm fallback; medic revives; restrain surrendered suspects; radio subtitles; labels and waypoint lines in the world; status on HUD.
- **Weapons:** seven primaries (compact SMG, SMG, compact rifle, service rifle with auto/semi, shotgun with pellet spread, precision carbine, less-lethal launcher) and three sidearms; hitscan, magazines and reserves, reload, switching, semi/auto, spread and recoil bloom, muzzle flash, impacts, tracer, dry fire, role restrictions; block models with a `modelPrefab` override slot.
- **Attachments:** weapon light, red dot, suppressor, two stocks, with small stat effects, configured as data.
- **Armor:** light, standard and heavy (reduction, speed, capacity, durability, helmet, color); ballistic shield with frontal protection, bracing and cover for teammates behind.
- **Equipment:** flashbang (radius, duration, line of sight, flash and sound), smoke (cheap sphere puffs that block AI sight and expire), breaching charge (designated doors only), door wedge (place/remove), medical kit (heal over time, short animation), portable light, recon camera (text panel, partial view); capacity-limited per officer with role discounts.
- **Suspects:** unarmed, armed hostile, guard, nervous, armored, leader (+ training dummy); state machine Idle, Patrol, Suspicious, Investigating, Alert, Chasing, Attacking, TakingCover, Searching, Fleeing, Hiding, Stunned, Surrendering, Restrained, Dead; hearing, sight with FOV/darkness/smoke/crouch, last known position, search, callouts, alarms, cover, surrender, leader escape, guards raising the alarm, questioning unarmed suspects reveals others.
- **Civilians:** office worker, security guard, visitor, injured, hiding, hostage, resident; idle, wander, panic, flee, hide, follow, wait, injured, captive, evacuated; tracked encountered/rescued/evacuated/injured/killed.
- **Doors and rooms:** Closed, Open, Locked, Wedged, Breached, Disabled; open/close, pick lock, unlock from console, kick, breach, wedge; status shown in the prompt; rooms Undiscovered -> Discovered -> Investigated -> Secured -> Complete, updated a few times per second (no physics triggers).
- **Maps:** Office Complex, Warehouse, Apartment Building (ground floor, second floor and roof linked by stairs), Training Ground, HQ with parked van; van arrival sequence on deployment.
- **Missions:** data-driven `MissionData` (training + four operations), mandatory and optional objectives, extraction, failure (team leader down, abort), consistent scoring with rating, briefing text built from the rolled variation.
- **Randomization:** seeded per deployment (shown, rerollable, keepable): spawn points from tagged pools, patrol routes, randomly locked doors (always breachable and pickable), optional objectives (only valid ones), alarm/camera state, power outage.
- **Tactical map and planning:** walls, doors by state, rooms by state, team, visible and last-known threats, discovered civilians, evidence, consoles, active cameras, zones, objective rooms, player markers, floor tabs; planning mode pauses or slows time, selects officers, places waypoints and markers, quick orders.
- **Security:** sweeping cameras that detect officers and trigger the alarm, alarm with beacons that alerts suspects, consoles to disable cameras/alarm, unlock electronic doors and review footage.
- **HUD:** health, armor, stamina, weapon, ammo, fire mode, reload bar, equipment strip, crosshair with spread, interaction prompt, objectives with progress, mission timer and tallies, squad health/orders/ammo, radio, notifications, alarm banner, threat count, hit direction, flashbang whiteout.
- **Progression and save:** missions completed, best scores and ratings, training completion, unlocks (attachments, two officers, a mission per milestone, uniforms), mission records, lifetime stats, loadouts, squad choice, settings and key bindings in one versioned JSON file.
- **Audio:** pooled `AudioManager` with Effects/Voice/Ambience/Interface categories, per-category volume and mute; procedural placeholder sounds for weapons, reloads, footsteps, doors, equipment, radio chirps, detection, alarms, civilians, UI, mission complete/fail; indoor room tone vs outdoor wind.
- **Settings:** quality preset (Auto + 5 tiers), FPS counter, volumes and mutes, difficulty, zoom sensitivity, look-ahead, default zoom preset, planning pause vs slow motion, line-of-sight fog, campaign reset, full key remapping with conflict swapping.
- **Editor tools:** create Boot scene, export editable data assets, open screenshots folder, delete save.

## Features simplified or deferred

- **Scenes, prefabs, materials and models are generated by code at runtime**
  instead of being authored as `.unity`/`.prefab`/material/model files. The
  spec asked for those to be created; generating them from code was chosen
  because this work was done without a running Unity editor, where hand-writing
  scene and prefab YAML is error-prone. The Boot scene is created by the editor
  script on first open. Separate scenes per map (`Mission_Office.unity`, etc.)
  were replaced by building each map into the one scene.
- **No bank map.** It appears only in the spec's suggested folder structure; three playable maps plus training were built.
- **Portraits and mission thumbnails** are drawn from flat shapes, not rendered images.
- **Animations** are procedural (pose blending, recoil, crouch, reload, fall) on primitive-based characters; no rigged models or animation clips. No weapon inspection animation.
- **Audio** is procedural placeholder sound. Radio and squad acknowledgments are text subtitles with a radio chirp; there is no voice acting.
- **Vehicles:** arrival sequence only; no mission-end return sequence, no driving.
- **Camera collision avoidance** is not implemented (walls are drawn low and the camera stays above them).
- **Multiple floors** are laid out side by side and linked by stair teleports. Squadmates cross floors when following you or escorting a civilian; suspects never change floors.
- **The team leader cannot be revived;** going down ends the mission.
- **Darkness on Potato/Low presets** (no dynamic lights) affects visibility rules but is not drawn as darker rooms.
- **Key remapping** uses the game's own binding table (read through the Input System when it is installed) rather than an Input Action asset and its rebinding UI. No gamepad support.
- **Recon camera** is a text summary, as the spec allows; no live rendering.
- **UI** is IMGUI drawn in a 1080-pixel-high virtual space. It is laid out for 16:9; narrow aspect ratios (4:3) may overlap panels.

## Testing performed

What was actually done:

1. **Compilation outside Unity.** All scripts under `Assets/Scripts` were
   compiled with the Mono C# compiler (`mcs`) against the UnityEngine module
   reference assemblies for Unity 2021.3.33 (NuGet package
   `unityengine.modules`), in three configurations:
   - default (classic Input Manager path);
   - `ENABLE_INPUT_SYSTEM` + `SWAT_INPUT_SYSTEM` against a hand-written stub of the Input System API surface the code uses;
   - `UNITY_EDITOR` against a hand-written stub of the UnityEditor APIs the editor script uses.

   Result: all three compile with **0 errors and 0 warnings**. Limits: the
   reference assemblies are Unity 2021.3, not Unity 6; the stubs only prove
   the code is consistent with the signatures I wrote into them; `mcs` is a
   different compiler from Unity's Roslyn compiler (the code sticks to C# 7
   features to stay compatible with both).
2. **Code review against the milestone acceptance criteria**, tracing the main
   flows by reading the code (deployment, squad stack and entry, objective
   tracking, scoring, save/progression, restart cleanup). Problems found and
   fixed during that review included: training steps that could soft-lock if
   done out of order, solo training with no squad, shield carriers unable to
   finish the weapon-switch step, stacking officers walking their own door
   open, stack positions inside the breaching charge's blast radius, the
   deployment cutscene revealing hidden suspects, menu buttons clickable under
   the settings window, and the screenshot notice appearing in the screenshot.

What was **not** done: opening the project in Unity, entering Play Mode,
checking the Unity Console, verifying NavMesh generation, testing any
gameplay, scene/prefab reference checks (there are none to check), building a
player, measuring performance, or capturing screenshots.

## Known issues and risks

- Untested at runtime. Expect bugs that only show in Play Mode: AI getting
  stuck on specific furniture, balance (suspect accuracy, damage, scoring),
  UI elements overlapping at some resolutions, and possible API behavior
  differences between Unity 2021.3 (compiled against) and Unity 6.
- An officer ordered to stack on a closed door from its far side walks
  through the closed door (the NavMesh is open there; only locked/wedged doors
  carve it).
- IMGUI draws each shape as its own draw call; the planning map can issue
  several hundred per frame. Fine on most hardware, but it is the most likely
  UI cost on very slow machines.
- Line-of-sight fog hides suspects your team can't see; with fog turned off
  in settings, all suspects are visible (intended, but it makes missions much easier).

## Performance considerations

- One `Update` in `AIManager` drives every suspect, civilian and squadmate;
  decisions run on a staggered timer (0.1 to 0.3 s depending on the preset), not every frame.
- Perception is distance and field-of-view first, then one line-of-sight
  raycast per candidate; the team's visibility pass runs five times a second.
- Hitscan raycasts for all firearms; grenades fly on a scripted arc without physics.
- Pooled tracers, debris, flash lights, grenades, smoke clouds and audio sources.
- Shared materials per color, primitive meshes, no textures beyond tiny UI ones.
- NavMesh baked once per deployment with a coarse voxel size; doors use carving obstacles.
- Graphics presets scale render resolution, shadows, dynamic lights, MSAA,
  effect counts and AI update rate; Auto mode steps down when the frame rate
  stays under 40 FPS.

## Exact instructions for opening and playing

1. Install Unity 6 (6000.0.x) with Unity Hub.
2. Unity Hub -> **Add -> Add project from disk** -> select this repository folder.
3. Wait for the import. Check that `Assets/Scenes/Boot.unity` exists; if not,
   run **SWAT -> Create Boot Scene**.
4. Open `Boot.unity`, press **Play**, click in the Game view.
5. Main menu -> **Training** to learn the controls, or **New Mission** to start Operation Glass Desk.
6. See [README.md](README.md) for controls and [Screenshots/README.md](Screenshots/README.md) for capturing the required screenshots.
